# ADR-0006: Provider credential and secret-handling policy

- **Status:** Accepted
- **Date:** 2026-05-23
- **Applies to:** All `Sentirum.Agent.Providers.*` packages

## Context

Every provider extension (`UseOpenAI`, `UseAnthropic`, `UseOllama`,
`UseZAI`, `UseOpenAICompatible`, `UseAnthropicCompatible`) takes an
optional `apiKey` parameter and falls back to a provider-specific
environment variable when none is supplied. Logging happens in
`SentirumChatClientBase` via source-generated `LoggerMessage`.

The M3 reviewer asked us to formalize:

1. Which env var each provider reads.
2. What gets logged about credentials.
3. How users can opt into / out of redaction.

## Decision

### Environment-variable fallbacks

| Provider                | Env var                  | Required? |
| ----------------------- | ------------------------ | --------- |
| OpenAI                  | `OPENAI_API_KEY`         | Yes (or arg) |
| Anthropic               | `ANTHROPIC_API_KEY`      | Yes (or arg) |
| Ollama                  | (none — endpoint only)   | No        |
| OpenAI-compatible       | provider-defined          | No (arg) |
| Anthropic-compatible    | provider-defined          | No (arg) |
| Z.AI                    | `ZAI_API_KEY`            | Yes (or arg) |

When the env var is unset and no `apiKey` was provided, the
extension throws `InvalidOperationException` with a message that
names the env var.

### Logging contract

- `SentirumChatClientLogging` **never logs the API key or the
  request body**. It logs the provider name, message count,
  duration, and (on failure) the exception type and message — the
  exception itself is recorded so structured logging consumers can
  capture the stack.
- Provider extensions **must not** log the raw `apiKey` argument.
  The validation message above mentions only the env-var name, not
  the value.
- Users who want full request/response logging can add their own
  delegating layer via `ConfigureChatClient(b => b.UseLogging(...))`
  — this is opt-in and explicitly off by default.

### Future: `SentirumCredentialProvider`

A `ICredentialProvider` abstraction is planned for M9 (hardening)
so users can plug in Azure Key Vault, AWS Secrets Manager, or
HashiCorp Vault without a process-wide env var. The current API
remains source-compatible.

## Consequences

- Predictable env-var names for every provider, documented in each
  provider package's README.
- Credentials never appear in default logs.
- Customers building regulated deployments have a clear opt-out:
  pass the key via DI, never read the env var, never call
  `UseLogging`.

## Alternatives considered

- **Force every provider to take a `ICredentialProvider`.** Too
  heavy for v0.1 when 90% of users want "read OPENAI_API_KEY".
- **Log the masked key (`sk-...XXXX`).** Even masked, recurring
  prefixes leak account identity into shared log sinks. Skip
  entirely.
