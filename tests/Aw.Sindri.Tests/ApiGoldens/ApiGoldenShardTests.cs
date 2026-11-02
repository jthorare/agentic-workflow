namespace Aw.Sindri.Tests.ApiGoldens;

public sealed class ApiGoldenShardTests
{
    public static TheoryData<string, int> Sharded() => new()
    {
        { "Context", 6 },
        { "Jobs", 2 },
        { "Launches", 2 },
        { "Items", 2 },
    };

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Sharded))]
    public void Shards_partition_the_rows_of_their_group(string group, int shards)
    {
        var union = Enumerable.Range(0, shards)
            .SelectMany(s => ApiGoldenRows.Of(group, s, shards).Select(r => r.Data))
            .ToList();

        Assert.Equal(union.Count, union.Distinct().Count());
        Assert.Equal(
            ApiGoldenRows.Of(group).Select(r => r.Data).Order(),
            union.Order());
    }
}
