# Sentirum.Agent.Observability

OpenTelemetry spans, cost tracking, and token budget enforcement for Sentirum agents.

## Packages

- `SentirumChatClientTelemetry` — emits `Activity` spans with `gen_ai.*` tags.
- `CostTrackingService` — accumulates cost and token usage per agent / per user.
- `TokenBudgetChatClient` — hard token budget with `TokenBudgetExceededException`.
- `PerModelCostModel` — simple price-per-million-tokens cost model.

## Usage

```csharp
services.AddSingleton<ICostModel>(PerModelCostModel.Create(
    ("gpt-4o-mini", new TokenPrice(InputPricePerMillion: 0.15m, OutputPricePerMillion: 0.60m)),
    ("gpt-4o", new TokenPrice(InputPricePerMillion: 2.50m, OutputPricePerMillion: 10.00m))
));

services.AddSentirumAgent("support", b => b
    .UseOpenAI("gpt-4o-mini", apiKey: key)
    .WithTelemetry()
    .WithCostTracking()
    .WithTokenBudget(maxTokens: 100_000));
```

## OpenTelemetry

Spans are emitted via `ActivitySource("Sentirum.Agent")`. Wire into your OTel pipeline:

```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("Sentirum.Agent"));
```
