using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Sentirum.Agent.Builder;
using Sentirum.Agent.Context;
using Sentirum.Agent.Memory;

namespace Sentirum.Agent;

/// <summary>
/// Discoverable builder extensions that register
/// <see cref="AIContextProvider"/> instances on an agent. They run inside
/// the deferred options pipeline (see ADR-0005) so they can resolve
/// services from DI via <see cref="SentirumServiceProviderAccessor.Current"/>.
/// </summary>
public static class SentirumContextBuilderExtensions
{
    /// <summary>
    /// Registers an <see cref="AIContextProvider"/> on the agent's pipeline.
    /// Order is preserved.
    /// </summary>
    public static ISentirumAgentBuilder WithContextProvider(
        this ISentirumAgentBuilder builder,
        AIContextProvider provider)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(provider);

        builder.Configure(options => options.ContextProviders.Add(provider));
        return builder;
    }

    /// <summary>
    /// Registers an <see cref="AIContextProvider"/> resolved from DI.
    /// </summary>
    public static ISentirumAgentBuilder WithContextProvider<TProvider>(this ISentirumAgentBuilder builder)
        where TProvider : AIContextProvider
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Configure(options =>
        {
            var sp = SentirumServiceProviderAccessor.Current
                ?? throw new InvalidOperationException(
                    "WithContextProvider<T>() requires a Sentirum service-provider scope. " +
                    "Register the agent through AddSentirumAgent(...).");
            options.ContextProviders.Add(sp.GetRequiredService<TProvider>());
        });
        return builder;
    }

    /// <summary>
    /// Adds ambient instructions that are computed per request. Useful for
    /// time-sensitive ("today's date is X"), user-specific, or
    /// session-specific instructions.
    /// </summary>
    public static ISentirumAgentBuilder WithAmbientInstructions(
        this ISentirumAgentBuilder builder,
        Func<MessageAIContextProvider.InvokingContext, CancellationToken, ValueTask<string?>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return builder.WithContextProvider(new InstructionsContextProvider(factory));
    }

    /// <summary>
    /// Convenience overload: synchronous ambient instructions.
    /// </summary>
    public static ISentirumAgentBuilder WithAmbientInstructions(
        this ISentirumAgentBuilder builder,
        Func<MessageAIContextProvider.InvokingContext, string?> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return builder.WithAmbientInstructions((ctx, _) => ValueTask.FromResult(factory(ctx)));
    }

    /// <summary>
    /// Injects every entry from a memory partition into the agent's
    /// instructions. The partition is resolved per request from the
    /// <see cref="AIContextProvider.InvokingContext"/>.
    /// </summary>
    public static ISentirumAgentBuilder WithMemoryContext(
        this ISentirumAgentBuilder builder,
        Func<MessageAIContextProvider.InvokingContext, MemoryPartition> partitionFactory,
        string heading = "Known facts:",
        int maxEntries = 20)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(partitionFactory);

        builder.Configure(options =>
        {
            var sp = SentirumServiceProviderAccessor.Current
                ?? throw new InvalidOperationException(
                    "WithMemoryContext() requires a Sentirum service-provider scope. " +
                    "Register the agent through AddSentirumAgent(...).");

            var store = sp.GetRequiredService<ISentirumMemoryStore>();
            options.ContextProviders.Add(new MemoryContextProvider(store, partitionFactory, heading, maxEntries));
        });
        return builder;
    }

    /// <summary>
    /// Convenience overload that targets a fixed user partition keyed off
    /// <paramref name="userId"/>. Prefer the
    /// <see cref="WithUserMemory(ISentirumAgentBuilder, Func{MessageAIContextProvider.InvokingContext, string}, string, int)"/>
    /// overload in multi-tenant hosts so the user id is resolved per
    /// request.
    /// </summary>
    public static ISentirumAgentBuilder WithUserMemory(
        this ISentirumAgentBuilder builder,
        string userId,
        string heading = "Known facts about the user:",
        int maxEntries = 20)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return builder.WithMemoryContext(_ => MemoryPartition.ForUser(userId), heading, maxEntries);
    }

    /// <summary>
    /// Multi-tenant overload: resolves the user id per request from the
    /// <see cref="MessageAIContextProvider.InvokingContext"/>. Callers
    /// typically read the id from an <c>IHttpContextAccessor</c>, an
    /// ambient request scope, or the active session.
    /// </summary>
    public static ISentirumAgentBuilder WithUserMemory(
        this ISentirumAgentBuilder builder,
        Func<MessageAIContextProvider.InvokingContext, string> userIdSelector,
        string heading = "Known facts about the user:",
        int maxEntries = 20)
    {
        ArgumentNullException.ThrowIfNull(userIdSelector);
        return builder.WithMemoryContext(
            ctx => MemoryPartition.ForUser(userIdSelector(ctx)),
            heading,
            maxEntries);
    }

    /// <summary>
    /// Multi-tenant overload: resolves the session id per request from the
    /// invoking context (typically a custom id stashed on
    /// <c>AgentSession.StateBag</c>).
    /// </summary>
    public static ISentirumAgentBuilder WithSessionMemory(
        this ISentirumAgentBuilder builder,
        Func<MessageAIContextProvider.InvokingContext, string> sessionIdSelector,
        string heading = "Session notes:",
        int maxEntries = 20)
    {
        ArgumentNullException.ThrowIfNull(sessionIdSelector);
        return builder.WithMemoryContext(
            ctx => MemoryPartition.ForSession(sessionIdSelector(ctx)),
            heading,
            maxEntries);
    }

    /// <summary>
    /// Injects ranked snippets from an <see cref="IKnowledgeBase"/> on every
    /// request, seeded from the latest user message on the session.
    /// </summary>
    public static ISentirumAgentBuilder WithKnowledgeBase(
        this ISentirumAgentBuilder builder,
        IKnowledgeBase knowledgeBase,
        int maxResults = 3,
        string heading = "Relevant knowledge-base entries:")
    {
        ArgumentNullException.ThrowIfNull(knowledgeBase);
        return builder.WithContextProvider(new KnowledgeBaseContextProvider(knowledgeBase, maxResults, heading));
    }

    /// <summary>
    /// Resolves <typeparamref name="TKnowledgeBase"/> from DI and wires it
    /// into the agent.
    /// </summary>
    public static ISentirumAgentBuilder WithKnowledgeBase<TKnowledgeBase>(
        this ISentirumAgentBuilder builder,
        int maxResults = 3,
        string heading = "Relevant knowledge-base entries:")
        where TKnowledgeBase : class, IKnowledgeBase
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Configure(options =>
        {
            var sp = SentirumServiceProviderAccessor.Current
                ?? throw new InvalidOperationException(
                    "WithKnowledgeBase<T>() requires a Sentirum service-provider scope. " +
                    "Register the agent through AddSentirumAgent(...).");

            var kb = sp.GetRequiredService<TKnowledgeBase>();
            options.ContextProviders.Add(new KnowledgeBaseContextProvider(kb, maxResults, heading));
        });
        return builder;
    }
}
