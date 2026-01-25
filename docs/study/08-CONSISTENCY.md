# 08 — Consistência e Idempotência

## Modelo de consistência
- **Eventual**: propagação do post para o feed ocorre em segundos.
- **At-least-once**: duplicatas são esperadas.

## Idempotência
- Fanout Worker deve tratar eventos repetidos sem duplicar itens no feed.
- ZSET permite inserção idempotente quando o member é único.
- Store de deduplicação (TTL) evita reprocessamento excessivo.

## Padrão Outbox (por que existe)
- Escrita do post e registro do evento acontecem na **mesma transação**.
- O publisher lê a outbox **depois do commit**, evitando perda de eventos.
- Em falhas do broker, o evento permanece pendente e será reenviado.
- Semântica final: **at-least-once** com possibilidade de duplicatas.

## Cursor e ordenação
- Itens ordenados por tempo, com **desempate estável**.
- Cursor guarda `score` + `tie-breaker`.

## Stubs
- [TODO] Definir formato do cursor e encoding.
