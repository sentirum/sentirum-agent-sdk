using Xunit;

namespace Sentirum.Agent.Embeddings.Tests;

public sealed class InMemoryVectorStoreTests
{
    private readonly InMemoryVectorStore<string> _store = new("test", 3);

    [Fact]
    public async Task Upsert_And_Get_RoundTrip()
    {
        var record = new VectorRecord<string>
        {
            Id = "r1",
            Vector = new[] { 1f, 0f, 0f },
            Text = "hello",
        };

        await _store.UpsertAsync(record);
        var got = await _store.GetAsync("r1");

        Assert.NotNull(got);
        Assert.Equal("r1", got.Id);
        Assert.Equal("hello", got.Text);
    }

    [Fact]
    public async Task Delete_Removes_Record()
    {
        await _store.UpsertAsync(new VectorRecord<string>
        {
            Id = "r2",
            Vector = new[] { 0f, 1f, 0f },
        });

        var deleted = await _store.DeleteAsync("r2");
        var got = await _store.GetAsync("r2");

        Assert.True(deleted);
        Assert.Null(got);
    }

    [Fact]
    public async Task Search_Returns_Ranked_By_Cosine()
    {
        await _store.UpsertAsync(new VectorRecord<string>
        {
            Id = "a",
            Vector = new[] { 1f, 0f, 0f },
            Text = "apple",
        });
        await _store.UpsertAsync(new VectorRecord<string>
        {
            Id = "b",
            Vector = new[] { 0f, 1f, 0f },
            Text = "banana",
        });

        var results = await _store.SearchAsync(new[] { 1f, 0f, 0f }, new VectorSearchOptions { TopK = 1 });

        Assert.Single(results);
        Assert.Equal("a", results[0].Record.Id);
        Assert.True(results[0].Score > 0.99f);
    }

    [Fact]
    public async Task Search_With_MinScore_Filters()
    {
        await _store.UpsertAsync(new VectorRecord<string>
        {
            Id = "a",
            Vector = new[] { 1f, 0f, 0f },
        });
        await _store.UpsertAsync(new VectorRecord<string>
        {
            Id = "b",
            Vector = new[] { 0f, 1f, 0f },
        });

        var results = await _store.SearchAsync(
            new[] { 1f, 0f, 0f },
            new VectorSearchOptions { TopK = 10, MinScore = 0.5f });

        Assert.Single(results);
    }

    [Fact]
    public async Task Count_And_Clear()
    {
        await _store.UpsertAsync(new VectorRecord<string>
        {
            Id = "x",
            Vector = new[] { 0f, 0f, 1f },
        });

        Assert.Equal(1, await _store.CountAsync());

        var cleared = await _store.ClearAsync();
        Assert.Equal(1, cleared);
        Assert.Equal(0, await _store.CountAsync());
    }

    [Fact]
    public async Task Wrong_Dimensions_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _store.UpsertAsync(new VectorRecord<string>
            {
                Id = "bad",
                Vector = new[] { 1f, 2f },
            }));
    }
}
