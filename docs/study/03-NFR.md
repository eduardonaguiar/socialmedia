# 03 — Requisitos Não-Funcionais (NFR)

## Lista de NFR
- **NFR-01** Baixa latência de leitura do feed (p95 < 400 ms local).
- **NFR-02** Alta disponibilidade com degradação graciosa.
- **NFR-03** Eventos com **at-least-once** (duplicatas possíveis).
- **NFR-04** Consistência eventual aceitável (segundos).
- **NFR-05** Consumidores **idempotentes**.
- **NFR-06** Observabilidade mínima (logs, métricas, traces).

## Trade-offs aceitos
- Leituras podem ficar defasadas por alguns segundos.
- Latência de escrita pode aumentar para garantir confiabilidade de eventos.

## Observabilidade — convenções mínimas
- **Atributos de recurso** obrigatórios: `service.name` e `deployment.environment=dev`.
- **Propagação de trace**: W3C Trace Context (`traceparent`).
- **Métricas**: duração de requisição (histograma), contagem total e contagem de erro.
- **Logs**: sempre que possível, incluir `trace_id` para correlação.
