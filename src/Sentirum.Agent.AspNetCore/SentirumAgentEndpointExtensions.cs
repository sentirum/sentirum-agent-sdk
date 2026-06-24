using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Sentirum.Agent.AspNetCore;

/// <summary>
/// Extension methods for mapping Sentirum agent endpoints onto
/// <see cref="IEndpointRouteBuilder"/>.
/// </summary>
public static class SentirumAgentEndpointExtensions
{
    /// <summary>
    /// Maps a POST endpoint that runs the named agent and returns a single
    /// aggregated JSON response.
    /// </summary>
    public static IEndpointConventionBuilder MapSentirumAgent(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        string agentName)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        return endpoints.MapPost(pattern, async (
            [FromBody] ChatMessage request,
            IServiceProvider services,
            CancellationToken ct) =>
        {
            var agent = services.GetRequiredKeyedService<ISentirumAgent>(agentName);
            var sessionStore = services.GetRequiredService<ISentirumSessionStore>();
            var session = await sessionStore.CreateAsync(agent.Id, ct).ConfigureAwait(false);

            var response = await agent.RunAsync(session, request, ct).ConfigureAwait(false);
            return Results.Json(response, statusCode: 200);
        });
    }

    /// <summary>
    /// Maps a POST endpoint that runs the named agent with streaming output
    /// via Server-Sent Events (<c>text/event-stream</c>).
    /// </summary>
    public static IEndpointConventionBuilder MapSentirumAgentStreaming(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        string agentName)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        return endpoints.MapPost(pattern, async (
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var agent = httpContext.RequestServices.GetRequiredKeyedService<ISentirumAgent>(agentName);
            var sessionStore = httpContext.RequestServices.GetRequiredService<ISentirumSessionStore>();

            var request = await JsonSerializer.DeserializeAsync<ChatMessage>(
                httpContext.Request.Body,
                JsonContext.Default.ChatMessage,
                ct).ConfigureAwait(false);

            if (request is null)
            {
                httpContext.Response.StatusCode = 400;
                return;
            }

            var session = await sessionStore.CreateAsync(agent.Id, ct).ConfigureAwait(false);

            httpContext.Response.ContentType = "text/event-stream";
            httpContext.Response.Headers.Append("Cache-Control", "no-cache");
            httpContext.Response.Headers.Append("Connection", "keep-alive");

            await using var writer = new StreamWriter(httpContext.Response.Body);

            var stream = agent.RunStreamingAsync(session, request, ct);
            await foreach (var update in stream.ConfigureAwait(false))
            {
                var json = JsonSerializer.Serialize(update, JsonContext.Default.ChatResponseUpdate);
                await writer.WriteLineAsync($"data: {json}").ConfigureAwait(false);
                await writer.WriteLineAsync().ConfigureAwait(false);
                await writer.FlushAsync(ct).ConfigureAwait(false);
            }
        });
    }
}

/// <summary>
/// JSON serialization context for the AspNetCore package.
/// </summary>
[JsonSerializable(typeof(ChatMessage))]
[JsonSerializable(typeof(ChatResponseUpdate))]
internal sealed partial class JsonContext : JsonSerializerContext
{
}
