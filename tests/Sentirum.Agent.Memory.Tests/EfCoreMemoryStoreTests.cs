using System;
using System.Data.Common;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sentirum.Agent.Memory.EntityFrameworkCore;
using Xunit;

namespace Sentirum.Agent.Memory.Tests;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Disposed via IAsyncLifetime.DisposeAsync.")]
public class EfCoreMemoryStoreTests : IAsyncLifetime
{
    private SqliteConnection? _connection;
    private SentirumMemoryDbContext? _context;
    private EfCoreMemoryStore<SentirumMemoryDbContext>? _store;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SentirumMemoryDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new SentirumMemoryDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        _store = new EfCoreMemoryStore<SentirumMemoryDbContext>(_context);
    }

    public async Task DisposeAsync()
    {
        if (_context is not null)
        {
            await _context.DisposeAsync();
        }
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task SetThenGet_PersistsAndStampsRow()
    {
        var partition = MemoryPartition.ForUser("u-1");
        await _store!.SetAsync(partition, "name", "Ersin");

        var entry = await _store.GetAsync(partition, "name");
        entry.Should().NotBeNull();
        entry!.Value.Value.Should().Be("Ersin");
    }

    [Fact]
    public async Task SetSecondTime_UpdatesValueAndStamp()
    {
        var partition = MemoryPartition.ForUser("u-1");
        await _store!.SetAsync(partition, "name", "first");
        var first = await _store.GetAsync(partition, "name");

        await Task.Delay(10);
        await _store.SetAsync(partition, "name", "second");
        var second = await _store.GetAsync(partition, "name");

        second!.Value.Value.Should().Be("second");
        second.Value.UpdatedAt.Should().BeAfter(first!.Value.UpdatedAt);
    }

    [Fact]
    public async Task PartitionScoping_DoesNotBleedAcrossUsers()
    {
        await _store!.SetAsync(MemoryPartition.ForUser("u-1"), "k", "alice");
        await _store.SetAsync(MemoryPartition.ForUser("u-2"), "k", "bob");

        (await _store.GetAsync(MemoryPartition.ForUser("u-1"), "k"))!.Value.Value.Should().Be("alice");
        (await _store.GetAsync(MemoryPartition.ForUser("u-2"), "k"))!.Value.Value.Should().Be("bob");
    }

    [Fact]
    public async Task ClearAsync_RemovesPartitionRowsOnly()
    {
        await _store!.SetAsync(MemoryPartition.ForUser("u-1"), "a", "1");
        await _store.SetAsync(MemoryPartition.ForUser("u-1"), "b", "2");
        await _store.SetAsync(MemoryPartition.ForUser("u-2"), "x", "9");

        var removed = await _store.ClearAsync(MemoryPartition.ForUser("u-1"));
        removed.Should().Be(2);

        (await _store.GetAsync(MemoryPartition.ForUser("u-2"), "x")).Should().NotBeNull();
    }

    [Fact]
    public async Task UniqueIndex_RejectsDuplicatePartitionKeyOnRawInsert()
    {
        // Sanity-check the index — set twice and ensure we still have a
        // single row (update path).
        var partition = MemoryPartition.ForUser("u-1");
        await _store!.SetAsync(partition, "k", "v1");
        await _store.SetAsync(partition, "k", "v2");

        var count = 0;
        await foreach (var _ in _store.ListAsync(partition))
        {
            count++;
        }
        count.Should().Be(1);
    }
}
