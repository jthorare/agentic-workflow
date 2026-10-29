namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>The Sandbox group of API goldens (<see cref="ApiScenarios.Groups"/>), a class of its own so it runs in parallel with the other groups.</summary>
public sealed class SandboxApiGoldenTests
{
    public static TheoryData<string, string> Rows() => ApiGoldenRows.Of("Sandbox");

    [Theory(Timeout = 30_000), Trait("Lane", "integration")]
    [MemberData(nameof(Rows))]
    public Task Api_answer_matches_its_golden_file(string slug, string scenario) => ApiGoldenRows.Match(slug, scenario, TestContext.Current.CancellationToken);
}
