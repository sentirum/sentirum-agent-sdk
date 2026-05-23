# Sentirum.Agent.Tools.Core

Tool authoring primitives for the **Sentirum Agent SDK**: the `[Tool]`
attribute, reflection-based discovery, and fluent builder integration on
top of `Microsoft.Extensions.AI.AIFunctionFactory`.

## Author a tool

```csharp
public sealed class OrderTools
{
    [Tool(Description = "Look up the status of a Sentirum customer order.")]
    public async Task<string> GetOrderStatusAsync(
        [Description("Order id, e.g. 'ORD-42'")] string orderId,
        CancellationToken cancellationToken = default)
    {
        // call a database / REST API / whatever
        return $"Order {orderId} shipped on 2026-01-12.";
    }
}
```

## Register on an agent

```csharp
services.AddSingleton<OrderTools>();

services.AddSentirumAgent("support", b => b
    .UseOpenAI("gpt-4o-mini")
    .WithTools<OrderTools>()
    .WithInstructions("You are a Sentirum customer support agent."));
```

`WithTools<TToolset>()` scans the type, picks every method decorated with
`[Tool]`, and converts each into an `AIFunction` via
`AIFunctionFactory.Create`. The toolset instance is resolved from DI on
build, so your tool methods can take constructor dependencies.

## License

MIT
