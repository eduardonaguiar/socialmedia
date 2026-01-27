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

## Considerações futuras (stubs)
- [TODO] Sharding de Redis por faixa de usuário.
- [TODO] Particionamento de tópicos por `author_id`.
- [TODO] Ajuste adaptativo de janelas por autor (post rate).
