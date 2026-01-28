# 11 — Evolução de Escala (1× → 10× → 100×)

Este capítulo fecha o ciclo de escala: quando a arquitetura atual basta, quando ela
precisa de alavancas obrigatórias e quando ela exige mudanças estruturais. O foco é
sempre **Following Feed**, com entregas **at-least-once** e dados derivados
reconstruíveis.

## 11.1 Premissas e sinais usados

**Premissas de produto (Case 1):**
- Feed cronológico (sem ranking/ML).
- `DAU` inicial ~1M (base do problema) e `MAU` ~10M.
- Hot window por usuário: **1.000 itens** (Redis ZSET).
- Threshold inicial de celebridade: **100k seguidores**.

**Sinais observáveis (para gatilhos de mudança):**
- **Lag do Kafka** no tópico de `PostCreated`.
- **p95/p99** de leitura do feed (NFR p95 < 400ms local).
- **Memória do Redis** e taxa de evicção/trimming.
- **Taxa de fanout**: eventos/seg × seguidores/evento (write amplification).
- **Erro 5xx / circuit breaker aberto** em Graph/Post/Redis.

> A evolução abaixo usa esses sinais como “gatilhos” de mudança — não “porque sim”.

---

## 11.2 A) MVP scale (≈ 1×)

**Assumptions:**
- DAU: ~1–5M
- Seguidores medianos < 100
- Poucas celebridades, baixo skew

**Características predominantes:**
- Fan-out (push) domina custo, mas ainda é controlável.
- Redis mantém hot window confortável (memória estável).
- Um grupo de workers (fanout) suficiente.
- Cache para pull de celebridades é quase irrelevante.

**Por que a arquitetura é simples intencionalmente:**
- Evita complexidade prematura (sharding, múltiplas regiões).
- Mantém a **autoridade** nos serviços de Post/Graph.
- Usa Redis apenas como **derivado rebuildable**.

**Salvaguardas que ainda NÃO são necessárias:**
- Sharding de Redis por buckets.
- Pipelines de merge pré-computadas.
- Multi-region read replicas.

---

## 11.3 B) Growth scale (≈ 10×)

**Assumptions:**
- DAU: ~20–50M
- Skew visível (power-law)
- Celebridades comuns e posts com milhões de seguidores

**Mudanças obrigatórias (já implementadas):**
- **Threshold de celebridade + feed híbrido (push/pull).**
- **Backpressure no fanout** (concorrência limitada + rate control).
- **Redis memory pressure awareness** (trim + TTL).
- **Kafka partition tuning** (distribuir autores quentes e reduzir lag).

**Gatilhos (exemplos):**
- `Kafka lag` persistente > 60–120s → reduzir fanout, revisar partições.
- `Redis evictions` subindo → reduzir `HOT_WINDOW_MAX_ITEMS` e/ou TTL.
- `p99` do feed > 2× SLO → abrir circuit breaker e retornar feed parcial.

**Como o híbrido estabiliza write amplification:**
- Posts de celebridade deixam de gerar **milhões de writes**.
- Custo desloca do write para o read (controlável com cache curto e janelas).
- Escalamos o fanout de “muitos seguidores” apenas para autores normais.

---

## 11.4 C) Hyper-scale (≈ 100×)

**Assumptions:**
- DAU: 100M+
- Skew extremo e tráfego global
- Padrões regionais fortes (latência/custos)

**Mudanças esperadas (documentadas, não implementadas):**
- **Sharding de feed** por buckets de usuário (Redis clusters/buckets).
- **Timelines dedicadas para celebridades** (pull mais eficiente).
- **Merge caches** pré-computados para celebridades mais quentes.
- **Feed hydration assíncrono** (por lote, com limites de frescor).
- **Read replicas regionais** para Graph/Post (reduzir p99 global).

**O que precisa mudar vs o que permanece:**
- **Muda:** armazenamento de hot window em múltiplos shards; fanout e pull se
  tornam multi-regionais; caches precisam de invalidação mais cuidadosa.
- **Permanece:** autoridade em Post/Graph; feed segue derivado; at-least-once
  com idempotência; híbrido por skew.

**O que vira organizacional:**
- SLOs por região (follow/read), governança de cache e limites de custo.
- Rotina de capacity planning (equipes + budget + observabilidade).

---

## 11.5 Análise de gargalos (postmortem-preventivo)

| Componente | Tipo de Gargalo | Sintoma | Mitigação |
| --- | --- | --- | --- |
| Redis feed ZSET | Memória / evicção | gaps no feed, churn alto | hot window + TTL + trim agressivo |
| Fanout worker | CPU / I/O | lag no Kafka, backlog | backpressure + skip celebridade |
| Graph Service | Amplificação de leitura | spikes de latência | materializações + cache curto |
| Feed Service | Fan-in no read | p99 alto, timeouts | circuit breakers + feed parcial |
| Post Service | Hot keys de escrita | contenção no DB | batching/sharding (futuro) |

> A leitura acima se comporta como checklist de prevenção: sintomas → ação.

---

## 11.6 Alavancas de controle (knobs)

**Levers já presentes no sistema:**
- `CELEBRITY_FOLLOWER_THRESHOLD`
- `HOT_WINDOW_MAX_ITEMS`
- Limites de concorrência no fanout (worker)
- TTL + jitter em caches de celebridades e timelines de autor
- `DEDUP_TTL_DAYS` (janela de idempotência)
- `FANOUT_FAILURE_BACKOFF_MS`
- Retries e circuit breakers (`*_RETRY`, `*_CIRCUIT_BREAKER`)

**Efeitos de ajuste:**
- Threshold de celebridade **alto demais** → writes explodem; **baixo demais** →
  leituras caras e cache pressionado.
- Hot window **alta demais** → Redis caro; **baixa demais** → feed “corto”.
- Concurrency **alta demais** → saturação de Graph/Redis; **baixa demais** → lag.
- TTL/jitter **alto demais** → frescor ruim; **baixo demais** → thundering herd.
- Retries **agressivos** → cascata; **restritivos** → perda de cobertura.

**Classificação de levers:**
- **Emergência:** circuit breakers, backoff, limiter do fanout.
- **Planejamento:** threshold de celebridade, hot window, TTLs.

---

## 11.7 Falha + escala (interação crítica)

- **Thundering herd piora** com cache curto + muitos seguidores.
- **Retries são perigosos** sob carga: multiplicam o trabalho e elevam p99.
- **Feed parcial** preserva disponibilidade (melhor que travar tudo).
- **Corretude degrada antes da disponibilidade** (CAP):
  - Redis pode ficar desatualizado, mas feed continua servindo algo.
  - Pull de celebridades pode degradar sem derrubar o feed.

**SLO thinking:** priorizar **p99** sobre perfeição, pois o usuário sente tempo
primeiro. A arquitetura foi feita para **degradar conteúdo antes de degradar
latência**.

---

## 11.8 Checklist rápido de evolução
- **Se lag do Kafka cresce** → reduzir fanout e aumentar partições.
- **Se memória do Redis satura** → reduzir hot window + TTL.
- **Se p99 explode** → abrir circuit breaker e servir feed parcial.
- **Se custo explode** → aumentar threshold de celebridade.
