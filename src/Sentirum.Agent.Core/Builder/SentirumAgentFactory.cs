using System;
using System.Collections.Generic;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Sentirum.Agent.Builder;

/// <summary>
/// Materializes a configured <see cref="ISentirumAgent"/> from a
/// <see cref="SentirumAgentBuilder"/> by composing the chat-client pipeline,
/// applying the options pipeline, and wrapping the resulting
/// <see cref="ChatClientAgent"/>.
/// </summary>
public static class SentirumAgentFactory
{
    /// <summary>
    /// Builds a <see cref="ISentirumAgent"/> using the registrations captured
    /// on the supplied <paramref name="builder"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no chat-client factory has been registered on the builder.
    /// At least one provider must call <see cref="ISentirumAgentBuilder.UseChatClient"/>.
    /// </exception>
    public static ISentirumAgent Create(
        SentirumAgentBuilder builder,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        if (builder.ChatClientFactory is null)
        {
            throw new InvalidOperationException(
                $"Sentirum agent '{builder.Name}' has no chat-client configured. " +
                "Call UseOpenAI(), UseAnthropic(), UseChatClient(...) or another " +
                "provider extension before resolving the agent.");
        }

        // 1. Resolve the options pipeline first so context providers can be
        // discovered before we build the chat-client pipeline. Deferred
        // extensions (such as WithTools<T>() in Sentirum.Agent.Tools.Core,
        // WithMemoryContext() in Sentirum.Agent.Context) need the DI scope
        // to be discoverable; we surface it via an AsyncLocal scope that
        // lives for the duration of the options pipeline.
        var options = new SentirumAgentOptions { Name = builder.Name };
        using (SentirumServiceProviderAccessor.Push(serviceProvider))
        {
            foreach (var configure in builder.OptionsConfigurations)
            {
                configure(options);
            }
        }

        // 2. Compose the IChatClient pipeline. The leaf client comes from the
        // provider factory; configured layers are wrapped around it in
        // registration order so the first layer ends up outermost.
        // Context providers are appended as a single UseAIContextProviders
        // call after user layers so they always sit closest to the leaf
        // chat client (i.e. context enrichment happens just before the LLM
        // is hit, after any user-supplied middleware has run).
        var chatClientBuilder = new ChatClientBuilder(builder.ChatClientFactory);
        foreach (var configure in builder.ChatClientLayers)
        {
            configure(chatClientBuilder);
        }

        if (options.ContextProviders.Count > 0)
        {
            var providers = new AIContextProvider[options.ContextProviders.Count];
            options.ContextProviders.CopyTo(providers, 0);
            chatClientBuilder.UseAIContextProviders(providers);
        }

        var chatClient = chatClientBuilder.Build(serviceProvider);

        // 3. Build the underlying MAF ChatClientAgent. Instructions are a ctor
        // argument (ChatClientAgentOptions does not expose them as a property
        // in MAF 1.6.x), so we use the multi-arg overload.
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
        var tools = options.Tools.Count > 0
            ? new List<AITool>(options.Tools)
            : null;

        var innerAgent = new ChatClientAgent(
            chatClient,
            instructions: options.Instructions,
            name: options.Name,
            description: options.Description,
            tools: tools,
            loggerFactory: loggerFactory,
            services: serviceProvider);

        return new SentirumAgent(
            id: builder.Name,
            name: options.Name,
            innerAgent: innerAgent);
    }
}
