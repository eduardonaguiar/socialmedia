# 09 — Falhas e Degradação

## Por que resiliente?
Mesmo com consistência eventual, o pipeline precisa **sobreviver** a falhas parciais e overload.
Quando um dependency cai, o objetivo é **degradar previsivelmente** (parcial) ao invés de colapsar o todo.

## Cenários de falha esperados
- **Kafka/Redpanda indisponível** → lag cresce; fan-out pausa/retoma.
- **Redis indisponível** → Feed Service responde **503** (falha explícita).
- **Graph Service indisponível** → feed só com janela push.
- **Post Service indisponível** → feed sem posts de celebridades (pull).
- **Postgres lento** → latência maior em criação/consulta de posts.

## Mecanismos implementados
### Backpressure (Fanout Worker)
- **Concorrência limitada** de eventos e writes no Redis.
- **Rate limits**: seguidores/seg e writes/seg.
- **Lag awareness**: se lag passa do threshold, consumo é pausado por janela curta.
- Sinal: `fanout_backpressure_applied_total{reason}` + `fanout_kafka_lag`.

### Retentativas com jitter (todos)
- Backoff exponencial com **jitter** (evita retry sincronizado).
- Limite de tentativas; nunca infinito.
- Métricas:
  - `retries_total{service,operation}`
  - `retry_exhausted_total{service,operation}`

### Circuit breakers (read path)
- Feed Service mantém breakers para **Graph** e **Post**.
- Após falhas consecutivas, o breaker abre e o feed **pula o pull**.
- Métricas:
  - `circuit_breaker_state{dependency}` (0=closed, 1=open, 2=half-open)
  - `circuit_breaker_open_total{dependency}`

### Degradação explícita (feed)
| Falha | Comportamento |
| --- | --- |
| Redis down | 503 explícito |
| Graph down | feed somente com push (ZSET) |
| Post down | feed sem celebridades (pull) |
| Múltiplas falhas | prioriza push; 503 apenas se Redis indisponível |

### Proteção contra thundering herd
- TTL com jitter nos caches de celebridades e timeline de autor.

### Timeouts explícitos
- HTTP clients (Graph/Post) com timeout curto.
- Redis com timeouts de conexão/comando.
- Kafka consume com poll timeout finito.
- Postgres com `command timeout` (evita consultas penduradas).
- Producer Kafka com timeout de envio para não bloquear outbox.

## Sinais para observabilidade
- `fanout_backpressure_applied_total` (rate limits, lag, falha).
- `retries_total` + `retry_exhausted_total`.
- `circuit_breaker_state` + `circuit_breaker_open_total`.
- `feed_partial_responses_total{reason}`.
- `fanout_kafka_lag`.
- `fanout_processing_duration_ms` sob carga.

## Caos local (manual)
### 1) Parar Redis
```bash
docker compose stop redis
```
**Esperado:** feed retorna 503; métricas de partial/erros Redis; recovery ao subir.

### 2) Parar Post Service
```bash
docker compose stop post-service
```
**Esperado:** feed sem posts de celebridade (push-only); breaker pode abrir.

### 3) Parar Graph Service
```bash
docker compose stop graph-service
```
**Esperado:** fanout falha e faz backoff; feed devolve push-only.

### 4) Pausar Redpanda
```bash
docker compose pause redpanda
```
**Esperado:** lag sobe; fanout reduz consumo; ao retomar, backlog drena.

### 5) Introduzir delay no Graph Service (simulado)
```bash
docker compose exec graph-service sh -c "apk add --no-cache iproute2 && tc qdisc add dev eth0 root netem delay 500ms"
```
**Esperado:** retries com jitter; breaker pode abrir se o delay persistir.

## Recuperação
- Subir serviços parados (`docker compose start <service>`).
- Confirmar métricas em Grafana (dashboard de observabilidade).
- Remover delay de rede: `docker compose exec graph-service tc qdisc del dev eth0 root`.
