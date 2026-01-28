# 10 — Escala e Skew

## Skew e power-law
- Poucos usuários concentram grande parte dos seguidores.
- Fan-out completo para celebridades é caro e gera hot keys.

## Estratégia inicial
- Threshold de celebridade evita fan-out total.
- Posts de celebridades são mesclados no read com janela limitada.
- Fanout Worker registra métricas de skip para visibilidade do ganho.

## Trade-offs operacionais
- **Write amplification menor** para posts de celebridades.
- **Read cost maior** para seguidores de celebridades (pull + merge).
- Cache curto mitiga picos, mas não elimina custo.
- Backpressure protege Redis e Graph ao limitar concorrência e taxa de fan-out.
- Circuit breakers no feed preservam p95/p99 quando dependências degradam.

## Testes k6 orientados a escala e resiliência
- O laboratório usa uma suíte k6 em `tests/k6/` para validar **carga de leitura/escrita**, **deduplicação** e **consistência eventual**.
- Cenários `scale-read` e `scale-write` exercitam hot keys e bursts para observar p95/p99 e sinais de backpressure.
- A execução local pode ser feita com `make k6` (usa `POST_SERVICE_URL`, `GRAPH_SERVICE_URL`, `FEED_SERVICE_URL`).
- Falhas esperadas são **duplicação**, **ordem inválida** e **perda silenciosa de dados** (o teste deve falhar alto).

## Considerações futuras (stubs)
- [TODO] Sharding de Redis por faixa de usuário.
- [TODO] Particionamento de tópicos por `author_id`.
- [TODO] Ajuste adaptativo de janelas por autor (post rate).
