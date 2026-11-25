namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>The Proposals group of API goldens (<see cref="ApiScenarios.Groups"/>): the proposal list, one proposal and the owner's reject, a class of its own so it runs in parallel with the other groups.</summary>
public sealed class ProposalsApiGoldenTests
{
    public static TheoryData<string, string> Rows() => ApiGoldenRows.Of("Proposals");

    [Theory(Timeout = 30_000), Trait("Lane", "integration")]
    [MemberData(nameof(Rows))]
    public Task Api_answer_matches_its_golden_file(string slug, string scenario) => ApiGoldenRows.Match(slug, scenario, TestContext.Current.CancellationToken);
}
