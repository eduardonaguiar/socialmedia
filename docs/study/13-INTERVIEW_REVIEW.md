# 13 — Revisão de Entrevista (Senior/Staff)

Este capítulo transforma o Case 1 em **material de entrevista**: o que um
entrevistador espera ouvir, quais são os sinais de senioridade e quais armadilhas
precisam ser evitadas.

## 13.1 Como um entrevistador avalia este design

### Sinais de pensamento **senior**
- Identifica **skew** e celebrações (power-law) cedo.
- Separa **dados autoritativos** vs **derivados**.
- Assume **at-least-once** e implementa idempotência.
- Defende consistência pragmática (feed pode atrasar, mas não quebra).

### Sinais de pensamento **staff**
- **Custo consciente**: foco em “fazer menos trabalho”.
- Evolução clara (1× → 10× → 100×) baseada em métricas.
- **Kill-switches e knobs**: consegue controlar o sistema em produção.
- **Degradação graciosa** (partial feed antes de falha total).

### Red flags clássicos
- “É só shardar” sem gatilho de mudança.
- “Exactly-once” em todos os lugares (custo irreal).
- Sem resposta para posts de celebridades.
- Sem discussão de custo.
- Sem análise de falhas e degradação.

---

## 13.2 Narrativa: “por que este design sobrevive em produção”

- **Autoridade é clara:** Post/Graph são fontes de verdade.
- **Derivados podem falhar:** Redis pode ser reconstruído.
- **Skew é tratado no core:** celebridades não explodem fanout.
- **Custos são controláveis:** knobs permitem ajustar write/read.
- **Falhas são esperadas:** circuit breakers + feed parcial.

---

## 13.3 Cheat sheet (1 página) — modelo mental final

### Fluxos principais
- **Write:** Post → Outbox → Kafka → Fanout Worker → Redis ZSET.
- **Read:** Feed Service → Redis hot window + pull de celebridades → merge.
- **Graph:** consultas de seguidores/celebridades via materializações.

### Consistência
- **Forte:** Post/Graph (autoridade).
- **Eventual:** Redis feed (derivado).
- **Pull de celebridades:** frescor depende de cache curto.

### Onde duplicamos (e por quê)
- **Redis ZSET**: derivado para reduzir leituras no DB.
- **Materializações no Graph**: acelerar consultas de seguidores.

### Disponibilidade vs frescor
- Em falha de Post/Graph: feed continua com push-only.
- Em falha de Redis: preferimos 503 explícito a servir dados incorretos.

### Pontos de evolução
- 1×: simples, barato, consistente.
- 10×: híbrido obrigatório + backpressure.
- 100×: shard + caches precomputados + multi-região.

---

## 13.4 Checklist de entrevista (10 minutos)
- Problema, escopo, dados autoritativos.
- Arquitetura base (Post/Graph/Feed/Fanout).
- Estratégia híbrida para celebridades.
- Métricas que disparam evolução (lag, p99, Redis memory).
- Custos e trade-offs.
- Falhas e degradação.
