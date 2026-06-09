using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Sentirum.Agent.AspNetCore;

/// <summary>
/// Extension methods for mapping A2A (Agent-to-Agent) protocol endpoints.
/// </summary>
public static class A2AEndpointExtensions
{
    /// <summary>
    /// Maps A2A protocol endpoints for a named agent:
    /// <list type="bullet">
    ///   <item><c>GET /.well-known/agent.json</c> — agent card</item>
    ///   <item><c>POST /a2a/tasks</c> — create task</item>
    ///   <item><c>GET /a2a/tasks/{id}</c> — get task status</item>
    ///   <item><c>GET /a2a/tasks/{id}/stream</c> — SSE streaming</item>
    /// </list>
    /// </summary>
    public static IEndpointRouteBuilder MapA2AEndpoints(
        this IEndpointRouteBuilder endpoints,
        string agentName,
        AgentCard card)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentNullException.ThrowIfNull(card);

        var taskStore = new InMemoryTaskStore();

        endpoints.MapGet("/.well-known/agent.json", () => Results.Json(card));

        endpoints.MapPost("/a2a/tasks", async (
            [FromBody] CreateTaskRequest request,
            IServiceProvider services,
            CancellationToken ct) =>
        {
            var agent = services.GetRequiredKeyedService<ISentirumAgent>(agentName);
            var sessionStore = services.GetRequiredService<ISentirumSessionStore>();
            var session = await sessionStore.CreateAsync(agent.Id, ct).ConfigureAwait(false);

            var taskId = Guid.NewGuid().ToString("n");
            var task = new A2ATask(taskId, request.Message, A2ATaskStatus.Submitted);
            taskStore.Add(task);

            _ = Task.Run(async () =>
            {
                try
                {
                    task.Status = A2ATaskStatus.Working;
                    var response = await agent.RunAsync(session, request.Message, default)
                        .ConfigureAwait(false);

                    task.Result = response.Messages.LastOrDefault()?.Text ?? string.Empty;
                    task.Status = A2ATaskStatus.Completed;
                }
                catch
                {
                    task.Status = A2ATaskStatus.Failed;
                }
            }, ct);

            return Results.Json(task, statusCode: 202);
        });

        endpoints.MapGet("/a2a/tasks/{id}", (string id) =>
        {
            var task = taskStore.Get(id);
            return task is null ? Results.NotFound() : Results.Json(task);
        });

        endpoints.MapGet("/a2a/tasks/{id}/stream", async (
            string id,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var task = taskStore.Get(id);
            if (task is null)
            {
                httpContext.Response.StatusCode = 404;
                return;
            }

            httpContext.Response.ContentType = "text/event-stream";
            httpContext.Response.Headers.Append("Cache-Control", "no-cache");

            await using var writer = new StreamWriter(httpContext.Response.Body);

            while (!ct.IsCancellationRequested && task.Status is A2ATaskStatus.Submitted or A2ATaskStatus.Working)
            {
                var json = JsonSerializer.Serialize(task, A2AJsonContext.Default.A2ATask);
                await writer.WriteLineAsync($"data: {json}").ConfigureAwait(false);
                await writer.WriteLineAsync().ConfigureAwait(false);
                await writer.FlushAsync(ct).ConfigureAwait(false);
                await Task.Delay(500, ct).ConfigureAwait(false);
            }

            // Final state
            var finalJson = JsonSerializer.Serialize(task, A2AJsonContext.Default.A2ATask);
            await writer.WriteLineAsync($"data: {finalJson}").ConfigureAwait(false);
            await writer.WriteLineAsync().ConfigureAwait(false);
            await writer.FlushAsync(ct).ConfigureAwait(false);
        });

        return endpoints;
    }
}

/// <summary>
/// Agent card metadata for A2A discovery.
/// </summary>
public sealed class AgentCard
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0";

    [JsonPropertyName("capabilities")]
    public AgentCapabilities Capabilities { get; init; } = new();
}

/// <summary>
/// A2A capabilities advertised in the agent card.
/// </summary>
public sealed class AgentCapabilities
{
    [JsonPropertyName("streaming")]
    public bool Streaming { get; init; } = true;

    [JsonPropertyName("pushNotifications")]
    public bool PushNotifications { get; init; } = false;
}

/// <summary>
/// Request body for creating an A2A task.
/// </summary>
public sealed class CreateTaskRequest
{
    [JsonPropertyName("message")]
    public required Microsoft.Extensions.AI.ChatMessage Message { get; init; }
}

/// <summary>
/// Represents an A2A task and its current state.
/// </summary>
public sealed class A2ATask
{
    [JsonPropertyName("id")]
    public string Id { get; }

    [JsonPropertyName("status")]
    public A2ATaskStatus Status { get; set; }

    [JsonPropertyName("message")]
    public Microsoft.Extensions.AI.ChatMessage Message { get; }

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    public A2ATask(string id, Microsoft.Extensions.AI.ChatMessage message, A2ATaskStatus status)
    {
        Id = id;
        Message = message;
        Status = status;
    }
}

/// <summary>
/// A2A task lifecycle states.
/// </summary>
public enum A2ATaskStatus
{
    Submitted,
    Working,
    Completed,
    Failed,
}

[JsonSerializable(typeof(AgentCard))]
[JsonSerializable(typeof(A2ATask))]
[JsonSerializable(typeof(CreateTaskRequest))]
internal sealed partial class A2AJsonContext : JsonSerializerContext
{
}

/// <summary>
/// In-memory store for A2A tasks.
/// </summary>
internal sealed class InMemoryTaskStore
{
    private readonly ConcurrentDictionary<string, A2ATask> _tasks = new(StringComparer.Ordinal);

    public void Add(A2ATask task) => _tasks[task.Id] = task;

    public A2ATask? Get(string id) => _tasks.TryGetValue(id, out var task) ? task : null;
}
