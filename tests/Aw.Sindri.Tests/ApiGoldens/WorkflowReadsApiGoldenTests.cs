namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>The WorkflowReads group of API goldens (<see cref="ApiScenarios.Groups"/>): the four workflow reads, a class of its own so it runs in parallel with the other groups.</summary>
public sealed class WorkflowReadsApiGoldenTests
{
    public static TheoryData<string, string> Rows() => ApiGoldenRows.Of("WorkflowReads");

    [Theory(Timeout = 30_000), Trait("Lane", "integration")]
    [MemberData(nameof(Rows))]
    public Task Api_answer_matches_its_golden_file(string slug, string scenario) => ApiGoldenRows.Match(slug, scenario, TestContext.Current.CancellationToken);
}
