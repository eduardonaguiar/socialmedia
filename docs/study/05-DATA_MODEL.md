# 05 — Modelo de Dados

## Tabelas autoritativas (PostgreSQL)
- **posts**
  - `post_id` (PK)
  - `author_id` (string)
  - `content` (texto)
  - `created_at_utc`
  - **limite de conteúdo**: 280 caracteres (API + CHECK)
- **outbox_messages**
  - `outbox_id` (PK, event_id)
  - `event_type`, `schema_version`
  - `payload_json`
  - `occurred_at_utc`, `published_at_utc`
  - `publish_attempts`, `last_error`
  - `lock_id`, `locked_at_utc`
- **follows**
  - `follower_id`
  - `followee_id`
  - `created_at`
  - (PK composta `follower_id`,`followee_id`)

## Mapa autoritativo vs derivado (Post Service)
- **Autoritativo**: `posts`, `outbox_messages` (fonte de verdade).
- **Derivado**: feeds no Redis e stores de deduplicação (rebuildáveis).

## Materializações derivadas
- **Redis ZSET**
  - Chave: `feed:{user_id}`
  - Score: timestamp do post (ms)
  - Member: `{post_id}:{author_id}` (para desempate)

## Outbox e idempotência
- **outbox_messages** (autoritativo)
  - Armazena eventos pendentes de publicação (`PostCreated v1`).
- **dedup_store** (derivado)
  - Guarda `event_id` processados (TTL) no Fanout.

## Campos e índices (stubs)
- [TODO] Definir índices e estratégias de paginação.
- [TODO] Especificar tamanho máximo do conteúdo.
