# Índice de Estudo — Case 1: Social Feed (Following Feed)

Este material é **auto-contido**, feito para impressão e estudo linear. O objetivo é
acompanhar o raciocínio de um **Following Feed** (não recomendação) com foco em
arquitetura, dados e consistência.

## Ordem de leitura
1. [01 — Problema e Escopo](01-PROBLEM.md)
2. [02 — Requisitos Funcionais (FR)](02-FR.md)
3. [03 — Requisitos Não-Funcionais (NFR)](03-NFR.md)
4. [04 — Arquitetura (Visão de Caixas)](04-ARCHITECTURE.md)
5. [05 — Modelo de Dados](05-DATA_MODEL.md)
6. [06 — Estratégia de Feed](06-FEED_STRATEGY.md)
7. [07 — Cache e Hot Window](07-CACHING.md)
8. [08 — Consistência e Idempotência](08-CONSISTENCY.md)
9. [09 — Falhas e Degradação](09-FAILURES.md)
10. [10 — Escala e Skew](10-SCALING.md)
11. [11 — Notas de Avaliação](11-EVAL_NOTES.md)

## Referências internas (EN)
- [Contexto do Projeto](../agent/PROJECT_CONTEXT.md)
- [Escopo FR/NFR](../agent/SCOPE.md)
- [Arquitetura](../agent/ARCHITECTURE.md)
- [Eventos](../agent/EVENTS.md)
- [Local Dev](../agent/LOCAL_DEV.md)
- [Observability](../agent/OBSERVABILITY.md)

## Convenções
- **Autoritativo vs Derivado** é um princípio obrigatório em todas as decisões.
- **Redis ZSET** guarda a hot window do feed.
- **At-least-once** para eventos: consumidores devem ser **idempotentes**.
