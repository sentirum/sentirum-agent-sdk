using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Sentirum.Agent.Memory.InMemory;
using Xunit;

namespace Sentirum.Agent.Memory.Tests;

public class InMemoryMemoryStoreTests
{
    [Fact]
    public async Task SetThenGet_RoundTripsValueAndStamps()
    {
        var store = new InMemoryMemoryStore();
        var partition = MemoryPartition.ForUser("u-1");

        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        await store.SetAsync(partition, "name", "Ersin");
        var entry = await store.GetAsync(partition, "name");

        entry.Should().NotBeNull();
        entry!.Value.Key.Should().Be("name");
        entry.Value.Value.Should().Be("Ersin");
        entry.Value.CreatedAt.Should().BeAfter(before);
        entry.Value.UpdatedAt.Should().BeAfter(before);
        entry.Value.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task PartitionScoping_BlocksCrossUserReads()
    {
        var store = new InMemoryMemoryStore();
        await store.SetAsync(MemoryPartition.ForUser("u-1"), "k", "alice");
        await store.SetAsync(MemoryPartition.ForUser("u-2"), "k", "bob");

        var alice = await store.GetAsync(MemoryPartition.ForUser("u-1"), "k");
        var bob = await store.GetAsync(MemoryPartition.ForUser("u-2"), "k");

        alice!.Value.Value.Should().Be("alice");
        bob!.Value.Value.Should().Be("bob");
    }

    [Fact]
    public async Task ExpiredEntry_IsNotReturnedAndIsLazilyRemoved()
    {
        var store = new InMemoryMemoryStore();
        var partition = MemoryPartition.ForUser("u-1");

        await store.SetAsync(partition, "k", "v", absoluteExpiration: DateTimeOffset.UtcNow.AddMilliseconds(50));
        await Task.Delay(100);

        var entry = await store.GetAsync(partition, "k");
        entry.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_ReturnsEveryNonExpiredEntry()
    {
        var store = new InMemoryMemoryStore();
        var partition = MemoryPartition.ForUser("u-1");
        await store.SetAsync(partition, "a", "1");
        await store.SetAsync(partition, "b", "2");
        await store.SetAsync(partition, "c", "3", absoluteExpiration: DateTimeOffset.UtcNow.AddMilliseconds(-1));

        var entries = new System.Collections.Generic.List<MemoryEntry>();
        await foreach (var entry in store.ListAsync(partition))
        {
            entries.Add(entry);
        }

        entries.Select(e => e.Key).Should().BeEquivalentTo("a", "b");
    }

    [Fact]
    public async Task GetJsonAsync_RoundTripsTypedPayload()
    {
        var store = new InMemoryMemoryStore();
        var partition = MemoryPartition.ForUser("u-1");

        var profile = new Profile("Ersin", 36, "Istanbul");
        await store.SetJsonAsync(partition, "profile", profile);

        var loaded = await store.GetJsonAsync<Profile>(partition, "profile");
        loaded.Should().Be(profile);
    }

    [Fact]
    public async Task ClearAsync_RemovesEveryEntryInPartition()
    {
        var store = new InMemoryMemoryStore();
        var partition = MemoryPartition.ForUser("u-1");
        await store.SetAsync(partition, "a", "1");
        await store.SetAsync(partition, "b", "2");

        var removed = await store.ClearAsync(partition);
        removed.Should().Be(2);

        var leftovers = 0;
        await foreach (var _ in store.ListAsync(partition))
        {
            leftovers++;
        }
        leftovers.Should().Be(0);
    }

    [Fact]
    public async Task SetAsync_Concurrent_OnSameKey_LastWriteWinsWithoutException()
    {
        var store = new InMemoryMemoryStore();
        var partition = MemoryPartition.ForUser("u-1");

        const int Writers = 64;
        var tasks = new Task[Writers];
        for (var i = 0; i < Writers; i++)
        {
            var v = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            tasks[i] = Task.Run(() => store.SetAsync(partition, "k", v));
        }
        await Task.WhenAll(tasks);

        var entry = await store.GetAsync(partition, "k");
        entry.Should().NotBeNull();
        var asInt = int.Parse(entry!.Value.Value, System.Globalization.CultureInfo.InvariantCulture);
        asInt.Should().BeInRange(0, Writers - 1);
    }

    [Fact]
    public async Task SetAsync_Update_PreservesCreatedAt()
    {
        var store = new InMemoryMemoryStore();
        var partition = MemoryPartition.ForUser("u-1");

        await store.SetAsync(partition, "k", "first");
        var first = (await store.GetAsync(partition, "k"))!.Value;

        await Task.Delay(10);
        await store.SetAsync(partition, "k", "second");
        var second = (await store.GetAsync(partition, "k"))!.Value;

        second.CreatedAt.Should().Be(first.CreatedAt, "updates must keep the original CreatedAt stamp.");
        second.UpdatedAt.Should().BeAfter(first.UpdatedAt);
    }

    [Fact]
    public async Task DeleteAsync_OnMissingKey_ReturnsFalse()
    {
        var store = new InMemoryMemoryStore();
        var removed = await store.DeleteAsync(MemoryPartition.ForUser("u-1"), "nope");
        removed.Should().BeFalse();
    }

    [Fact]
    public async Task ClearAsync_OnMissingPartition_ReturnsZero()
    {
        var store = new InMemoryMemoryStore();
        var removed = await store.ClearAsync(MemoryPartition.ForUser("u-never"));
        removed.Should().Be(0);
    }

    [Theory]
    [InlineData(MemoryScope.Global, "a", null, null)]
    [InlineData(MemoryScope.Global, null, "u", null)]
    [InlineData(MemoryScope.Agent, "a", "u", null)]
    [InlineData(MemoryScope.User, "a", "u", null)]
    [InlineData(MemoryScope.Session, null, "u", "s")]
    public void Validate_RejectsOverSpecifiedPartitions(MemoryScope scope, string? agentId, string? userId, string? sessionId)
    {
        var partition = new MemoryPartition(scope, agentId, userId, sessionId);
        var act = partition.Validate;
        act.Should().Throw<InvalidOperationException>().WithMessage("*must not specify*");
    }

    private sealed record Profile(string Name, int Age, string City);
}
