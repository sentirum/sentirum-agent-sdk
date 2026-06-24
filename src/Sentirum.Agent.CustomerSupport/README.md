# Sentirum.Agent.CustomerSupport

Customer support vertical for Sentirum.Agent.

## Usage

```csharp
services.AddCustomerSupport();

services.AddSentirumAgent("classifier", b => b
    .UseOpenAI("gpt-4o-mini", apiKey: key)
    .WithInstructions("Classify support tickets into categories..."));

services.AddSentirumAgent("refund-specialist", b => b
    .UseOpenAI("gpt-4o-mini", apiKey: key)
    .WithInstructions("Recommend refund amounts..."));

// Build triage workflow
var workflow = SupportWorkflowBuilder.CreateTriageWorkflow("triage", new[]
{
    registry.Find("refund-specialist")!,
    registry.Find("replacement-specialist")!,
    registry.Find("discount-specialist")!,
});

// Amount-based approval gate
var gate = SupportApprovalGate.ForAmount("support-approval", threshold: 100m);
```

## Packages

- `SupportTicket` / `SupportTicketStatus` — domain model
- `ISupportTicketStore` / `InMemorySupportTicketStore` — ticket storage
- `SupportWorkflowBuilder` — triage workflow factory + recommendation parsers
- `SupportApprovalGate` — pre-configured amount-threshold approval gate
- `AddCustomerSupport()` — DI registration
