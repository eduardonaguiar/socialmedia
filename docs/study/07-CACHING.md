# 07 — Cache e Hot Window

## Redis ZSET (hot window)
- Cada usuário tem um ZSET `case1:feed:{user_id}`.
- Mantém os **últimos 1.000 itens** (baseline) por usuário.
- Score = `created_at_ms` (epoch em ms), member = `post_id`.

## Políticas
- **Evicção**: remove itens mais antigos ao ultrapassar o limite.
  - Após inserir, usar `ZREMRANGEBYRANK key 0 -(max_items+1)`
  - Rank do ZSET é **ascendente** (0 = item mais antigo).
- **TTL**: opcional para liberar memória após inatividade.

## Cache no read híbrido
- **Lista de celebridades seguidas**: cache curto (60–300s) no Feed Service.
- **Timeline por autor (celebridade)**: cache curto (15–60s) para reduzir thundering herd.
- TTLs com **jitter** para evitar expiração simultânea.
- Cache não é fonte de verdade; serve apenas para aliviar chamadas.

## Degradação
- Nesta fase, o Feed Service retorna **503** quando Redis está indisponível,
  para tornar a falha explícita e mensurável.
- Se o pull falhar, retornar somente os itens do ZSET (feed parcial).
