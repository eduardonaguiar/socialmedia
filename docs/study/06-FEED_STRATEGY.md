# 06 — Estratégia de Feed

## Estratégias possíveis
- **Push**: fan-out na escrita (materializa feeds rapidamente).
- **Pull**: monta feed no read (consulta posts dos seguidos).
- **Híbrida**: push para usuários “normais”, pull para celebridades.

## Estratégia adotada (híbrida)
- **Push** para autores normais (fan-out no `PostCreated` → Redis ZSET).
- **Pull/Merge** para celebridades (sem fan-out para milhões de seguidores).
- **Graph Service**: mantém `followers_count` e expõe classificação por threshold.

### Threshold de celebridade
- Configurável via `CELEBRITY_FOLLOWER_THRESHOLD` (default **100.000**).
- Se `followers_count >= threshold`, o autor é tratado como celebridade.

## Escopo desta tarefa (híbrido)
- Fanout Worker **pula** fan-out para celebridades.
- Feed Service faz **pull** das timelines de celebridades seguidas e faz **merge** com ZSET.
- Post Service fornece endpoint de timeline por autor (cursor + desempate).

## Paginação por cursor
- Ordenação por **timestamp (score = created_at_ms)**.
- Desempate por **post_id** (ordem lexicográfica).
- Cursor contém `(score, member)` e é **opaco** (base64 JSON).
- Estratégia: buscar `score < last_score` + `score == last_score` com `member < last_member`.
- Limites: default **20**, máximo **100**.

## Pipeline do fan-out (push)
1. Post Service emite `PostCreated v1` (outbox).
2. Fanout Worker consome o evento (at-least-once).
3. Worker consulta `followers_count` no Graph Service.
4. Se **celebridade**: **skip** fan-out e registra métrica.
5. Se **normal**: busca seguidores no Graph Service (paginação por cursor).
6. Para cada seguidor: `ZADD case1:feed:{follower_id} score=created_at_ms member=post_id`.
7. Trim do hot window para manter somente os **N** itens mais recentes.

## Pipeline do read híbrido (merge)
1. Feed Service lê a página do ZSET do usuário (push).
2. Busca lista de **celebridades seguidas** no Graph Service (cache curto).
3. Para cada celebridade, busca últimos N posts no Post Service (cache curto).
4. Faz merge determinístico (`created_at_ms DESC`, `post_id DESC`) com dedupe.
5. Retorna página e cursor global `(score, post_id)`.

Limites assumidos:
- Merge usa janela de posts por celebridade (ex: últimos 20) e janela temporal (ex: 48h).
- Paginação profunda pode reconsultar janelas (trade-off explícito).

Observação: existe um limite opcional (`FOLLOWER_MAX_PAGES`) apenas para proteção em laboratório;
por padrão o worker não aplica cap de páginas.

## Validação local (exemplo)
```bash
curl -X POST http://localhost:8082/follow/author-a -H 'X-User-Id: follower-b'
curl -X POST http://localhost:8081/posts -H 'X-User-Id: author-a' -H 'Content-Type: application/json' \
  -d '{"content":"hello"}'
curl -H 'X-User-Id: follower-b' http://localhost:8083/feed?limit=10
```

## Decisões importantes
- **Sem fan-out** para celebridades reduz explosão de escrita.
- **Mais custo no read** para seguidores de celebridades (pull + merge).
