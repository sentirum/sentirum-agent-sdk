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

    private sealed record Profile(string Name, int Age, string City);
}
