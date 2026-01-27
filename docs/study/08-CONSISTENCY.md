# 08 — Consistência e Idempotência

## Modelo de consistência
- **Eventual**: propagação do post para o feed ocorre em segundos.
- **At-least-once**: duplicatas são esperadas.
- **Feed derivado**: Redis é reconstruível; ausência de item não invalida a verdade no Post Service.
- **Híbrido**: celebridades são lidas no read; consistência depende de pull recente.

## Idempotência
- Fanout Worker deve tratar eventos repetidos sem duplicar itens no feed.
- ZSET permite inserção idempotente quando o member é único.
- Store de deduplicação (TTL) evita reprocessamento excessivo.
- Estratégia atual: Redis `SET dedup:post_created:{event_id} NX EX <ttl>`.
  - TTL padrão: **7 dias**.
  - Se falhar antes de aplicar fan-out, o worker remove a chave para evitar perda.

## Consistência no modo híbrido
- Celebridades **não** são fanoutadas → leitura depende do Post Service.
- Se o pull falhar, o Feed Service retorna **feed parcial** (apenas ZSET).
- Ordenação determinística com desempate por `post_id` evita reordenação.
- Cursor global `(created_at_ms, post_id)` filtra ambos os sources.

## Padrão Outbox (por que existe)
- Escrita do post e registro do evento acontecem na **mesma transação**.
- O publisher lê a outbox **depois do commit**, evitando perda de eventos.
- Em falhas do broker, o evento permanece pendente e será reenviado.
- Semântica final: **at-least-once** com possibilidade de duplicatas.

## Cursor e ordenação
- Itens ordenados por tempo, com **desempate estável**.
- Cursor guarda `score` + `member` e é base64 JSON opaco.
- Para scores iguais, aplica-se `member < last_member` para evitar repetição.

## Compromisso de processamento
- Offset Kafka só é confirmado após: dedup claim + fan-out + trim bem-sucedidos.
- Em falha de Graph/Redis, o evento é **reprocessado** (at-least-once).

## Rebuild e reconciliação
- Redis pode ser reconstruído via replay de eventos.
- Pull de celebridades é **autoridade de leitura** para esses autores.
