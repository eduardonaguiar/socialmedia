# 10 — Escala e Skew

## Skew e power-law
- Poucos usuários concentram grande parte dos seguidores.
- Fan-out completo para celebridades é caro e gera hot keys.

## Estratégia inicial
- Threshold de celebridade evita fan-out total.
- Posts de celebridades são mesclados no read.

## Considerações futuras (stubs)
- [TODO] Sharding de Redis por faixa de usuário.
- [TODO] Particionamento de tópicos por `author_id`.
- [TODO] Limites de paginação para reduzir custo de merge.
