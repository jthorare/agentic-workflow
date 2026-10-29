namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>Shard 5 of 6 of the Context group of API goldens (<see cref="ApiGoldenRows.Of(string, int, int)"/>), a class of its own so it runs in parallel with the other shards.</summary>
public sealed class ContextApiGoldenShard5Tests
{
    public static TheoryData<string, string> Rows() => ApiGoldenRows.Of("Context", 5, 6);

    [Theory(Timeout = 30_000), Trait("Lane", "integration")]
    [MemberData(nameof(Rows))]
    public Task Api_answer_matches_its_golden_file(string slug, string scenario) => ApiGoldenRows.Match(slug, scenario, TestContext.Current.CancellationToken);
}
