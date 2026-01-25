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

## Escopo desta tarefa (read path)
- Implementa **apenas leitura** do feed via Redis ZSET (hot window).
- Não há fan-out neste momento (isso é feito na tarefa 07).
- O feed retorna **referências** (`post_id`), sem hidratação de conteúdo.

## Paginação por cursor
- Ordenação por **timestamp (score = created_at_ms)**.
- Desempate por **post_id** (ordem lexicográfica).
- Cursor contém `(score, member)` e é **opaco** (base64 JSON).
- Estratégia: buscar `score < last_score` + `score == last_score` com `member < last_member`.
- Limites: default **20**, máximo **100**.

## Stubs para implementação
- [TODO] Definir algoritmo de merge para celebridades (read híbrido).
