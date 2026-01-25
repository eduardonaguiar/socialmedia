# 04 — Arquitetura (Visão de Caixas)

## Descrição textual do diagrama
1. **Post Service (autoritativo)**
   - Grava posts no PostgreSQL.
   - Emite evento `PostCreated` via outbox.
2. **Graph Service (autoritativo)**
   - Mantém relações de follow (seguindo/seguidores).
   - Mantém materializações explícitas (saída/entrada) para leitura eficiente.
   - Responde consultas de seguidores e seguindo com paginação por cursor.
3. **Fanout Worker (derivado)**
   - Consome `PostCreated`.
   - Resolve seguidores do autor.
   - Atualiza feeds no Redis ZSET (janela quente).
4. **Feed Service (derivado)**
   - Lê Redis ZSET e aplica paginação por cursor.
   - Mescla posts de celebridades em tempo de leitura.
5. **Event Bus (Kafka/Redpanda)**
   - Transporta eventos com semântica at-least-once.

## Fronteiras autoritativas vs derivadas
- **Autoritativas**: dados de posts e relações de follow no PostgreSQL.
- **Derivadas**: feeds materializados no Redis e caches auxiliares.

## Contratos de integração
- Evento principal: `post.created.v1`.
- Consumidores devem ser idempotentes (duplicatas são esperadas).

## Base local (Docker Compose)
Para estudo local, iniciamos dependências centrais e observabilidade via Docker Compose:

- **PostgreSQL** (dados autoritativos).
- **Redis** (janela quente do feed, derivado).
- **Redpanda** (API Kafka para eventos).

## Observabilidade (stack local)
Para validar comportamento e depurar falhas desde o início, o stack local inclui:

- **OpenTelemetry Collector** como ponto único de entrada OTLP (gRPC/HTTP).
- **Prometheus** para métricas (coleta o próprio Prometheus + métricas internas do Collector).
- **Grafana** para visualização (datasources provisionados).
- **Loki** para logs agregados.
- **Tempo** para traces distribuídos.

Contrato básico:
- Serviços enviam OTLP para `otel-collector:4317` (gRPC) ou `otel-collector:4318` (HTTP).
- Métricas OTLP são expostas pelo Collector em `:8889` para o Prometheus fazer scrape.
- Logs OTLP são roteados para o Loki.
- Traces OTLP são roteados para o Tempo.

Convenções do stack local:
- Rede única `case1-net` para comunicação previsível.
- Volumes nomeados `pg_data`, `redpanda_data` e `redis_data`.
- Healthchecks rápidos para suportar `depends_on: condition: service_healthy`.
- Portas expostas para desenvolvimento: 5432 (Postgres), 6379 (Redis), 9092 (Kafka) e 9644 (admin/health Redpanda).
- `.env` centraliza credenciais/hosts para manter ergonomia.

## Pontos de atenção (stubs)
- [TODO] Detalhar partições do tópico e chaveamento.
- [TODO] Definir política de TTL na hot window.
