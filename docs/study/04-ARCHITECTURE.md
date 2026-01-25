# 04 — Arquitetura (Visão de Caixas)

## Descrição textual do diagrama
1. **Post Service (autoritativo)**
   - Grava posts no PostgreSQL.
   - Emite evento `PostCreated` via outbox.
2. **Graph Service (autoritativo)**
   - Mantém relações de follow (seguindo/seguidores).
   - Responde consultas de seguidores e seguindo.
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
Para estudo local, iniciamos apenas as dependências centrais via Docker Compose:

- **PostgreSQL** (dados autoritativos).
- **Redis** (janela quente do feed, derivado).
- **Redpanda** (API Kafka para eventos).

Convenções do stack local:
- Rede única `case1-net` para comunicação previsível.
- Volumes nomeados `pg_data`, `redpanda_data` e `redis_data`.
- Healthchecks rápidos para suportar `depends_on: condition: service_healthy`.
- Portas expostas para desenvolvimento: 5432 (Postgres), 6379 (Redis), 9092 (Kafka) e 9644 (admin/health Redpanda).
- `.env` centraliza credenciais/hosts para manter ergonomia.

## Pontos de atenção (stubs)
- [TODO] Detalhar partições do tópico e chaveamento.
- [TODO] Definir política de TTL na hot window.
