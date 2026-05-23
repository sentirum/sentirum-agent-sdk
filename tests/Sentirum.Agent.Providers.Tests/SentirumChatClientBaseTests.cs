using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Sentirum.Agent.Providers.Custom;
using Xunit;

namespace Sentirum.Agent.Providers.Tests;

public sealed class SentirumChatClientBaseTests
{
    [Fact]
    public async Task GetResponseAsync_RetriesOnTransientFailure()
    {
        var client = new FlakyChatClient(failuresBeforeSuccess: 2, reply: "ok");

        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")]);

        response.Text.Should().Be("ok");
        client.AttemptCount.Should().Be(3); // 2 failures + 1 success
    }

    [Fact]
    public async Task GetResponseAsync_PropagatesAfterMaxRetriesExhausted()
    {
        var client = new FlakyChatClient(failuresBeforeSuccess: int.MaxValue, reply: "never");

        var act = async () => await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")]);

        await act.Should().ThrowAsync<InvalidOperationException>();
        client.AttemptCount.Should().Be(4); // 1 initial + 3 retries (default MaxRetries)
    }

    [Theory]
    [InlineData(-1, 1000, 1)] // negative timeout
    [InlineData(1, 1000, -1)] // negative MaxRetries
    [InlineData(1, -1, 1)]    // negative RetryBaseDelay
    public void Constructor_RejectsInvalidOptions(int timeoutSecs, int retryDelayMs, int maxRetries)
    {
        var options = new SentirumChatClientOptions
        {
            ProviderName = "x",
            Timeout = TimeSpan.FromSeconds(timeoutSecs),
            RetryBaseDelay = TimeSpan.FromMilliseconds(retryDelayMs),
            MaxRetries = maxRetries,
            LogRequests = false,
        };

        var act = () => new FlakyChatClient(0, "x", options);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task GetResponseAsync_RespectsCallerCancellation()
    {
        var client = new FlakyChatClient(failuresBeforeSuccess: int.MaxValue, reply: "never");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () => await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        // Cancellation should be observed before the first attempt is even started.
        client.AttemptCount.Should().Be(0);
    }

    // Fails the first `failuresBeforeSuccess` calls and then succeeds.
    private sealed class FlakyChatClient : SentirumChatClientBase
    {
        private readonly int _failuresBeforeSuccess;
        private readonly string _reply;

        public int AttemptCount { get; private set; }

        public FlakyChatClient(int failuresBeforeSuccess, string reply)
            : this(failuresBeforeSuccess, reply, new SentirumChatClientOptions
            {
                ProviderName = "flaky",
                LogRequests = false,
                RetryBaseDelay = TimeSpan.FromMilliseconds(1),
                Timeout = TimeSpan.FromSeconds(5),
            })
        {
        }

        public FlakyChatClient(int failuresBeforeSuccess, string reply, SentirumChatClientOptions options)
            : base(options, NullLogger.Instance)
        {
            _failuresBeforeSuccess = failuresBeforeSuccess;
            _reply = reply;
        }

        protected override Task<ChatResponse> CallProviderAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options,
            CancellationToken cancellationToken)
        {
            AttemptCount++;
            if (AttemptCount <= _failuresBeforeSuccess)
            {
                throw new InvalidOperationException("simulated transient failure");
            }

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _reply)));
        }

        protected override async IAsyncEnumerable<ChatResponseUpdate> CallProviderStreamingAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            AttemptCount++;
            yield return new ChatResponseUpdate(ChatRole.Assistant, _reply);
            await Task.CompletedTask;
        }
    }
}
