# Sentirum.Agent.Tools.Mcp

Model Context Protocol (MCP) integration for Sentirum.Agent.

## Usage

### Stdio transport

```csharp
using ModelContextProtocol.Client;

var transport = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "Everything",
    Command = "npx",
    Arguments = ["-y", "@modelcontextprotocol/server-everything"],
});

services.AddSentirumAgent("myAgent", b => b
    .UseOpenAI("gpt-4o", apiKey: key)
    .WithMcpTools(transport));
```

### Pre-connected client

```csharp
var mcpClient = await McpClient.CreateAsync(transport);
var tools = await mcpClient.ListToolsAsync();

services.AddSentirumAgent("myAgent", b => b
    .UseOpenAI("gpt-4o", apiKey: key)
    .WithMcpTools(mcpClient));
```

### DI-resolved transport

```csharp
services.AddSingleton<IClientTransport>(sp => new StdioClientTransport(...));

services.AddSentirumAgent("myAgent", b => b
    .UseOpenAI("gpt-4o", apiKey: key)
    .WithMcpTools(sp => sp.GetRequiredService<IClientTransport>()));
```

## How it works

`McpToolsChatClient` is a <c>DelegatingChatClient</c> that:
1. Connects to the MCP server on first chat request (lazy)
2. Caches the discovered tools
3. Merges them into <c>ChatOptions.Tools</c> on every request

Because `McpClientTool` inherits from `AIFunction`, the tools work seamlessly with any `IChatClient` provider.
