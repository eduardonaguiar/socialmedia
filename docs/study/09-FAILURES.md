# 09 — Falhas e Degradação

## Cenários de falha esperados
- **Kafka/Redpanda indisponível** → atraso na propagação.
- **Redis indisponível** → Feed Service responde **503** (falha explícita).
- **Postgres lento** → latência maior em criação/consulta de posts.

## Estratégias de mitigação
- Retentativas com backoff no worker (Graph/Redis).
- Idempotência para suportar duplicatas.
- Dedup não é confirmada permanentemente se o fan-out falhar (evita perda).
- Queda graciosa com mensagens claras ao cliente (código 503 no feed).

## Sinais para observabilidade
- Lag do consumidor.
- Taxa de falha no fanout.
- Tempo de resposta de feed.
- Volume de escrita no Redis (fan-out).

## Stubs
- [TODO] Definir alertas e thresholds locais.
