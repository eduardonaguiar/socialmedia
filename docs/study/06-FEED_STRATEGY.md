# 06 — Estratégia de Feed

## Estratégias possíveis
- **Push**: fan-out na escrita (materializa feeds rapidamente).
- **Pull**: monta feed no read (consulta posts dos seguidos).
- **Híbrida**: push para usuários “normais”, pull para celebridades.

## Estratégia adotada (baseline)
- **Push** para usuários normais nesta fase (fan-out no `PostCreated` → Redis ZSET).
- **Híbrida** com threshold de celebridade será introduzida nas próximas tarefas.
- **Graph Service**: fornece lista de seguidores (materialização de entrada) para fan-out.

## Escopo desta tarefa (push path)
- Implementa **fan-out** no `PostCreated` para seguidores.
- ZSET em Redis armazena a **hot window** do feed (`case1:feed:{user_id}`).
- O feed continua retornando **referências** (`post_id`), sem hidratação de conteúdo.

## Paginação por cursor
- Ordenação por **timestamp (score = created_at_ms)**.
- Desempate por **post_id** (ordem lexicográfica).
- Cursor contém `(score, member)` e é **opaco** (base64 JSON).
- Estratégia: buscar `score < last_score` + `score == last_score` com `member < last_member`.
- Limites: default **20**, máximo **100**.

## Pipeline do fan-out (push)
1. Post Service emite `PostCreated v1` (outbox).
2. Fanout Worker consome o evento (at-least-once).
3. Worker busca seguidores no Graph Service (paginação por cursor).
4. Para cada seguidor: `ZADD case1:feed:{follower_id} score=created_at_ms member=post_id`.
5. Trim do hot window para manter somente os **N** itens mais recentes.

Observação: existe um limite opcional (`FOLLOWER_MAX_PAGES`) apenas para proteção em laboratório;
por padrão o worker não aplica cap de páginas.

## Validação local (exemplo)
```bash
curl -X POST http://localhost:8082/follow/author-a -H 'X-User-Id: follower-b'
curl -X POST http://localhost:8081/posts -H 'X-User-Id: author-a' -H 'Content-Type: application/json' \
  -d '{"content":"hello"}'
curl -H 'X-User-Id: follower-b' http://localhost:8083/feed?limit=10
```

## Stubs para implementação
- [TODO] Definir algoritmo de merge para celebridades (read híbrido).
