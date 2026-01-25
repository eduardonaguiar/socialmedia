# 07 — Cache e Hot Window

## Redis ZSET (hot window)
- Cada usuário tem um ZSET `feed:{user_id}`.
- Mantém os **últimos 1.000 itens** (baseline) por usuário.
- Score = `created_at` (ms), member = `post_id:author_id`.

## Políticas
- **Evicção**: remove itens mais antigos ao ultrapassar o limite.
- **TTL**: opcional para liberar memória após inatividade.

## Degradação
- Se Redis estiver indisponível, o feed pode cair para modo **pull parcial**
  (limitado) até o serviço se recuperar.

## Stubs
- [TODO] Definir TTL de ZSET e política de compactação.
