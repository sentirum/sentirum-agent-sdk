using System;
using FluentAssertions;
using Sentirum.Agent.Memory.Redis;
using Xunit;

namespace Sentirum.Agent.Memory.Tests;

public class RedisEnvelopeCodecTests
{
    [Fact]
    public void RoundTrip_PreservesValueCreatedUpdatedExpiresAt()
    {
        var ca = DateTimeOffset.UtcNow.AddMinutes(-10);
        var ua = DateTimeOffset.UtcNow.AddMinutes(-1);
        var ea = DateTimeOffset.UtcNow.AddDays(7);

        var encoded = RedisMemoryStore.EnvelopeCodec.Encode("hello", ca, ua, ea);
        var decoded = RedisMemoryStore.EnvelopeCodec.Decode("k", encoded);

        decoded.Key.Should().Be("k");
        decoded.Value.Should().Be("hello");
        decoded.CreatedAt.ToUnixTimeMilliseconds().Should().Be(ca.ToUnixTimeMilliseconds());
        decoded.UpdatedAt.ToUnixTimeMilliseconds().Should().Be(ua.ToUnixTimeMilliseconds());
        decoded.ExpiresAt!.Value.ToUnixTimeMilliseconds().Should().Be(ea.ToUnixTimeMilliseconds());
    }

    [Fact]
    public void RoundTrip_PreservesSurrogatePairCharacters()
    {
        // U+1F600 (grinning face emoji) — high surrogate D83D + low DE00.
        // A hand-rolled JSON escaper that processes char-by-char without
        // surrogate handling poisons this payload.
        const string Original = "Hello \uD83D\uDE00 world — Türkçe çay ☕";

        var encoded = RedisMemoryStore.EnvelopeCodec.Encode(
            Original,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null);

        var decoded = RedisMemoryStore.EnvelopeCodec.Decode("k", encoded);
        decoded.Value.Should().Be(Original);
        decoded.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void RoundTrip_PreservesControlCharactersAndEscapes()
    {
        const string Original = "tab\there\nnewline\"quote\\backslash";

        var encoded = RedisMemoryStore.EnvelopeCodec.Encode(
            Original,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null);

        var decoded = RedisMemoryStore.EnvelopeCodec.Decode("k", encoded);
        decoded.Value.Should().Be(Original);
    }
}
