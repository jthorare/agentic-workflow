namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>The Links group of API goldens (<see cref="ApiScenarios.Groups"/>): QUERY /sindri/links/resolve with no row and with a PR's row, a class of its own so it runs in parallel with the other groups.</summary>
public sealed class LinksApiGoldenTests
{
    public static TheoryData<string, string> Rows() => ApiGoldenRows.Of("Links");

    [Theory(Timeout = 30_000), Trait("Lane", "integration")]
    [MemberData(nameof(Rows))]
    public Task Api_answer_matches_its_golden_file(string slug, string scenario) => ApiGoldenRows.Match(slug, scenario, TestContext.Current.CancellationToken);
}
