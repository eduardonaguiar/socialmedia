# 05 — Modelo de Dados

## Tabelas autoritativas (PostgreSQL)
- **posts**
  - `post_id` (PK)
  - `author_id`
  - `content` (texto)
  - `created_at`
- **follows**
  - `follower_id`
  - `followee_id`
  - `created_at`
  - (PK composta `follower_id`,`followee_id`)

## Materializações derivadas
- **Redis ZSET**
  - Chave: `feed:{user_id}`
  - Score: timestamp do post (ms)
  - Member: `{post_id}:{author_id}` (para desempate)

## Outbox e idempotência
- **outbox_posts** (autoritativo)
  - Armazena eventos pendentes de publicação.
- **dedup_store** (derivado)
  - Guarda `event_id` processados (TTL).

## Campos e índices (stubs)
- [TODO] Definir índices e estratégias de paginação.
- [TODO] Especificar tamanho máximo do conteúdo.
