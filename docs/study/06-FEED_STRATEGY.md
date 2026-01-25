# 06 — Estratégia de Feed

## Estratégias possíveis
- **Push**: fan-out na escrita (materializa feeds rapidamente).
- **Pull**: monta feed no read (consulta posts dos seguidos).
- **Híbrida**: push para usuários “normais”, pull para celebridades.

## Estratégia adotada (baseline)
- **Híbrida** com **threshold de celebridade = 100k seguidores**.
- **Usuários normais**: fan-out no `PostCreated` → Redis ZSET.
- **Celebridades**: não fan-out total; posts são mesclados no read.
- **Graph Service**: fornece lista de seguidores (materialização de entrada) para fan-out.

## Paginação por cursor
- Ordenação por **timestamp (score)**.
- Desempate por **post_id** (ou `post_id+author_id`).
- Cursor contém `(score, tie-breaker)`.

## Stubs para implementação
- [TODO] Definir algoritmo de merge para celebridades.
- [TODO] Definir limites de página e window size.
