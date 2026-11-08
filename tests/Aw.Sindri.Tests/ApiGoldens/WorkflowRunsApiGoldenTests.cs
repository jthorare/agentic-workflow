namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>The WorkflowRuns group of API goldens (<see cref="ApiScenarios.Groups"/>): the refusals of 'wf run' and 'wf cancel', a class of its own so it runs in parallel with the other groups.</summary>
public sealed class WorkflowRunsApiGoldenTests
{
    public static TheoryData<string, string> Rows() => ApiGoldenRows.Of("WorkflowRuns");

    [Theory(Timeout = 30_000), Trait("Lane", "integration")]
    [MemberData(nameof(Rows))]
    public Task Api_answer_matches_its_golden_file(string slug, string scenario) => ApiGoldenRows.Match(slug, scenario, TestContext.Current.CancellationToken);
}
