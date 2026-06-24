using System;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;

namespace Sentirum.Agent.Tools.Mcp;

/// <summary>
/// Builder extensions for adding MCP server tools to a Sentirum agent.
/// </summary>
public static class SentirumMcpBuilderExtensions
{
    /// <summary>
    /// Adds tools from an MCP server to the agent. The MCP client is created
    /// lazily on the first chat request and tools are cached for subsequent
    /// requests.
    /// </summary>
    /// <param name="builder">The agent builder.</param>
    /// <param name="transport">The MCP transport (stdio, HTTP, etc.).</param>
    public static ISentirumAgentBuilder WithMcpTools(
        this ISentirumAgentBuilder builder,
        IClientTransport transport)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(transport);

        builder.ConfigureChatClient(b => b.Use(client =>
            new McpToolsChatClient(client, transport)));

        return builder;
    }

    /// <summary>
    /// Adds tools from an MCP server to the agent using a transport resolved
    /// from the service provider.
    /// </summary>
    /// <param name="builder">The agent builder.</param>
    /// <param name="transportFactory">Factory that resolves the transport from DI.</param>
    public static ISentirumAgentBuilder WithMcpTools(
        this ISentirumAgentBuilder builder,
        Func<IServiceProvider, IClientTransport> transportFactory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(transportFactory);

        builder.ConfigureChatClient(b => b.Use((client, services) =>
            new McpToolsChatClient(client, transportFactory(services))));

        return builder;
    }

    /// <summary>
    /// Adds a pre-connected MCP client's tools to the agent.
    /// </summary>
    /// <param name="builder">The agent builder.</param>
    /// <param name="client">The already-connected MCP client.</param>
    public static ISentirumAgentBuilder WithMcpTools(
        this ISentirumAgentBuilder builder,
        McpClient client)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(client);

        builder.ConfigureChatClient(b => b.Use(inner =>
            new McpToolsChatClient(inner, client)));

        return builder;
    }
}
