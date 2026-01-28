# 12 — Modelo de Custos (qualitativo)

Este modelo é intencionalmente **qualitativo** e centrado nos principais drivers.
O objetivo é pensar como Staff: **onde o dinheiro vai** e **como reduzir trabalho**.

## 12.1 Drivers de custo

1. **Redis (memória)**
   - ZSET por usuário ativo + TTL.
   - Custo cresce com `HOT_WINDOW_MAX_ITEMS` e DAU ativo.

2. **CPU de fanout**
   - Custo ≈ posts/seg × seguidores/post (write amplification).
   - Explode com celebridades se for push puro.

3. **Kafka/Redpanda throughput**
   - Volume de eventos (`PostCreated`) + consumo retardado.
   - Lag implica mais workers e/ou partições.

4. **Chamadas cross-service (pull)**
   - Read merge para celebridades (Graph + Post).
   - Custo cresce com seguidores de celebridades e janelas longas.

---

## 12.2 Por que push-only explode o custo
- Cada post “quente” gera **milhões de writes**.
- Write amplification cresce linearmente com seguidores.
- Redis, CPU e Kafka escalam juntos → custo não linear.

## 12.3 Por que híbrido controla o custo
- Evita fanout para celebridades (maior parcela do skew).
- Custo desloca para **reads sob demanda**, com cache curto.
- Mantém Redis pequeno e previsível.

## 12.4 Por que Redis é mais barato que DB reads em escala
- ZSET local responde mais rápido e barato que querys complexas.
- DB fica protegida do fanout e de leitura repetida.
- Redis é **derivado**: pode falhar e ser reconstruído.

---

## 12.5 Regras de bolso (heurísticas)

> **Importante:** valores são relativos, não absolutos.

- **Custo por post** ≈ seguidores normais impactados + cache/merge se celebridade.
- **Custo por DAU** ≈ hot window (Redis) + leituras p95.
- **Custo por seguidor de celebridade** ≈ chamadas de pull + merge por leitura.

Se precisar reduzir custo rapidamente, faça **menos trabalho**:
- Aumente `CELEBRITY_FOLLOWER_THRESHOLD`.
- Reduza `HOT_WINDOW_MAX_ITEMS`.
- Limite fanout concorrente (backpressure).

---

## 12.6 Trade-offs explícitos
- **Menos write** → mais read (celebridades).
- **Menos memória** → feed “curto”.
- **Mais cache** → menos frescor (TTL alto).
- **Mais retries** → mais custo sob falha.

---

## 12.7 O que NÃO construir ainda
- Sistemas de ranking/ML.
- Multi-region write.
- Merge caches complexos (apenas se custo for crítico).

> A decisão é econômica: construir só quando métricas mostram necessidade.
