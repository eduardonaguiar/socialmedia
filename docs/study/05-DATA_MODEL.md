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
- **follow_edges**
  - `follower_id`
  - `followed_id`
  - `followed_at_utc`
  - (PK composta `follower_id`,`followed_id`)
  - CHECK: `follower_id <> followed_id`

## Mapa autoritativo vs derivado (Post Service)
- **Autoritativo**: `posts`, `outbox_messages` (fonte de verdade).
- **Derivado**: feeds no Redis e stores de deduplicação (rebuildáveis).

## Materializações do Social Graph (autoritativo)
- **following_by_user** (saída)
  - `user_id` (quem segue)
  - `followed_id`
  - `followed_at_utc`
  - (PK composta `user_id`,`followed_id`)
  - índice para `ORDER BY followed_at_utc DESC, followed_id DESC`
- **followers_by_user** (entrada)
  - `user_id` (quem é seguido)
  - `follower_id`
  - `followed_at_utc`
  - (PK composta `user_id`,`follower_id`)
  - índice para `ORDER BY followed_at_utc DESC, follower_id DESC`

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
- [TODO] Definir índices adicionais conforme crescimento.
- [TODO] Especificar tamanho máximo do conteúdo.

## Paginação por cursor (Social Graph)
- Ordenação determinística: `followed_at_utc DESC` + `id DESC` como desempate.
- Cursor opaco (base64 JSON): `{ "ts": "...", "id": "..." }`.
- Limite padrão: 50, máximo: 200.
- Mudanças concorrentes podem causar pequenas variações entre páginas (normal).
