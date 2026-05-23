using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent;

/// <summary>
/// Default <see cref="ISentirumAgent"/> implementation that wraps a
/// Microsoft Agent Framework <see cref="AIAgent"/>.
/// </summary>
/// <remarks>
/// <para>
/// Ownership model (see ADR-0001):
/// </para>
/// <list type="bullet">
///   <item><description>
///     The composed <see cref="IChatClient"/> pipeline is owned by the agent
///     and disposed when the agent is disposed. The leaf provider client
///     (e.g. <c>OpenAIClient</c>, <c>OllamaApiClient</c>) is reachable
///     through the pipeline and disposed as part of the same chain.
///   </description></item>
///   <item><description>
///     The wrapped <see cref="AIAgent"/> is disposed only if it implements
///     <see cref="IAsyncDisposable"/> or <see cref="IDisposable"/>.
///     <c>ChatClientAgent</c> in MAF 1.6.x disposes its inner chat client
///     for us, so we avoid double disposal by leaving the pipeline to it.
///   </description></item>
/// </list>
/// </remarks>
public sealed class SentirumAgent : ISentirumAgent, IDisposable
{
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SentirumAgent"/> class.
    /// </summary>
    public SentirumAgent(string id, string name, AIAgent innerAgent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(innerAgent);

        Id = id;
        Name = name;
        InnerAgent = innerAgent;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public AIAgent InnerAgent { get; }

    /// <inheritdoc />
    public async Task<AgentResponse> RunAsync(
        ISentirumSession session,
        ChatMessage input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(input);
        ThrowIfDisposed();

        return await InnerAgent.RunAsync(
            [input],
            session.InnerSession,
            options: null,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AgentResponseUpdate> RunStreamingAsync(
        ISentirumSession session,
        ChatMessage input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(input);
        ThrowIfDisposed();

        var stream = InnerAgent.RunStreamingAsync(
            [input],
            session.InnerSession,
            options: null,
            cancellationToken);

        await foreach (var update in stream.ConfigureAwait(false))
        {
            yield return update;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        switch (InnerAgent)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }

    /// <summary>
    /// Synchronous dispose path so the DI container can shut down even when
    /// it is registered as a non-async scope. Prefer <see cref="DisposeAsync"/>
    /// when calling manually.
    /// </summary>
    /// <remarks>
    /// When the inner agent only implements <see cref="IAsyncDisposable"/>
    /// we offload to the thread pool (<c>Task.Run</c>) before blocking so
    /// the await does not capture the caller's <see cref="SynchronizationContext"/>
    /// and deadlock in environments such as WPF, WinForms, or ASP.NET
    /// (non-Core).
    /// </remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        if (InnerAgent is IDisposable disposable)
        {
            disposable.Dispose();
        }
        else if (InnerAgent is IAsyncDisposable asyncDisposable)
        {
            // Offload to the thread pool so the await inside DisposeAsync
            // cannot capture the caller's SynchronizationContext.
            Task.Run(() => asyncDisposable.DisposeAsync().AsTask())
                .GetAwaiter()
                .GetResult();
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
}
