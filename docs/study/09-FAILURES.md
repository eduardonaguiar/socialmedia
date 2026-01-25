# 09 — Falhas e Degradação

## Cenários de falha esperados
- **Kafka/Redpanda indisponível** → atraso na propagação.
- **Redis indisponível** → leitura em modo pull parcial ou erro controlado.
- **Postgres lento** → latência maior em criação/consulta de posts.

## Estratégias de mitigação
- Retentativas com backoff no worker.
- Idempotência para suportar duplicatas.
- Queda graciosa com mensagens claras ao cliente.

## Sinais para observabilidade
- Lag do consumidor.
- Taxa de falha no fanout.
- Tempo de resposta de feed.

## Stubs
- [TODO] Definir alertas e thresholds locais.
