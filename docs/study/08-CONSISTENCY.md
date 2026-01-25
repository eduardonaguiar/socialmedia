# 08 — Consistência e Idempotência

## Modelo de consistência
- **Eventual**: propagação do post para o feed ocorre em segundos.
- **At-least-once**: duplicatas são esperadas.

## Idempotência
- Fanout Worker deve tratar eventos repetidos sem duplicar itens no feed.
- ZSET permite inserção idempotente quando o member é único.
- Store de deduplicação (TTL) evita reprocessamento excessivo.

## Cursor e ordenação
- Itens ordenados por tempo, com **desempate estável**.
- Cursor guarda `score` + `tie-breaker`.

## Stubs
- [TODO] Definir formato do cursor e encoding.
