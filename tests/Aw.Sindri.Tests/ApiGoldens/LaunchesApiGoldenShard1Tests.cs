namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>Shard 1 of 2 of the Launches group of API goldens (<see cref="ApiGoldenRows.Of(string, int, int)"/>), a class of its own so it runs in parallel with the other shards.</summary>
public sealed class LaunchesApiGoldenShard1Tests
{
    public static TheoryData<string, string> Rows() => ApiGoldenRows.Of("Launches", 1, 2);

    [Theory(Timeout = 30_000), Trait("Lane", "integration")]
    [MemberData(nameof(Rows))]
    public Task Api_answer_matches_its_golden_file(string slug, string scenario) => ApiGoldenRows.Match(slug, scenario, TestContext.Current.CancellationToken);
}
