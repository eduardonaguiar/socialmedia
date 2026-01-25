# 01 — Problema e Escopo

## Objetivo do caso
Projetar e implementar localmente (docker-compose) um **Following Feed** semelhante a
Twitter/Instagram/LinkedIn, **sem recomendação**. O foco é entregar um feed de postagens
somente texto com consistência eventual e boa latência de leitura.

## Escopo (o que entra)
- Criar post textual e recuperar por ID.
- Criar/remover follow e consultar seguidores/seguindo.
- Gerar feed paginado por cursor (score + desempate).
- Pipeline de fan-out com eventos.
- Estratégia híbrida para usuários “celebridade”.

## Fora de escopo (o que NÃO entra)
- Recomendação, ML, ranking avançado.
- Upload de mídia, CDN, transcodificação.
- Autenticação completa e gestão de contas.
- Ads, moderação, busca, hashtags, tendências.

## Premissas fixas (assumptions travadas)
- **DAU**: 1 milhão
- **MAU**: 10 milhões
- **Relação leitura/escrita**: 100:1
- **Seguidores por usuário (média)**: 200
- **Seguindo por usuário (média)**: 200
- **Threshold de celebridade (inicial)**: 100k seguidores
- **Tamanho da hot window por usuário**: 1.000 itens (Redis ZSET)

> Estas premissas podem ser revisadas via ADR caso haja conflito futuro.

## Métricas-alvo (baseline local)
- p95 leitura de feed: < 400 ms (ambiente local)
- Consistência eventual: segundos para propagação

## Questões em aberto (para próximos passos)
- Definição final de limites de paginação e TTLs.
- Detalhes de chaveamento de partições e shards.
