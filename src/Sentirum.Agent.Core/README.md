# Sentirum.Agent.Core

Runtime for the **Sentirum Agent SDK**: the default `ISentirumAgent`
implementation, the fluent builder, linear sessions, and the helpers that
glue Microsoft Agent Framework together with the Sentirum abstractions.

Most consumers don't reference `Core` directly — they reference
`Sentirum.Agent.Hosting` plus a provider package and pick everything up
through dependency injection.

## License

MIT
