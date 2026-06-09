# Sentirum.Agent.Security

Security middleware for Sentirum agents.

## Packages

- `PiiRedactionChatClient` — regex-based PII stripping (email, phone, credit card, SSN, IPv4).
- `ContentSafetyChatClient` — pluggable content safety scanning via `IContentSafetyClient`.
- `SentirumSecurityBuilderExtensions` — `WithPiiRedaction()`, `WithContentSafety()`.

## Usage

```csharp
services.AddSentirumAgent("support", b => b
    .UseOpenAI("gpt-4o-mini", apiKey: key)
    .WithPiiRedaction()
    .WithContentSafety(ContentSafetyThreshold.Medium));

// Register an IContentSafetyClient implementation
services.AddSingleton<IContentSafetyClient, AzureContentSafetyClient>();
```

## Custom redaction rules

```csharp
var rules = new[]
{
    new RedactionRule("employee-id", new Regex(@"EMP-\d{6}", RegexOptions.Compiled)),
};

services.AddSentirumAgent("hr", b => b
    .UseOpenAI("gpt-4o-mini", apiKey: key)
    .WithPiiRedaction(rules, replacement: "[HIDDEN]"));
```
