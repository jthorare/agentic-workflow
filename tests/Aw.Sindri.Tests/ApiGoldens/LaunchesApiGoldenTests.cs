namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>The Launches group of API goldens (<see cref="ApiScenarios.Groups"/>), shard 0 of 2 (the others are the Shard classes beside it), a class of its own so it runs in parallel with the other groups.</summary>
public sealed class LaunchesApiGoldenTests
{
    public static TheoryData<string, string> Rows() => ApiGoldenRows.Of("Launches", 0, 2);

    [Theory(Timeout = 30_000), Trait("Lane", "integration")]
    [MemberData(nameof(Rows))]
    public Task Api_answer_matches_its_golden_file(string slug, string scenario) => ApiGoldenRows.Match(slug, scenario, TestContext.Current.CancellationToken);
}
