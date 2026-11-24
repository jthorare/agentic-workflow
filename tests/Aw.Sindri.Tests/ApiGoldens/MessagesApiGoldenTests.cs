namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The Messages group of API goldens (<see cref="ApiScenarios.Groups"/>, messages spec §14 M1f): the conversation list, a conversation window and a
/// thread, a class of its own so it runs in parallel with the other groups. The four list rows run under the spec's test name,
/// Messages.MessagesReadRouteTests.ApiGoldens_messages_lists, so no row runs twice.
/// </summary>
public sealed class MessagesApiGoldenTests
{
    public static TheoryData<string, string> Rows()
    {
        var rows = new TheoryData<string, string>();
        foreach (var (slug, scenario) in MessageScenarios.Built.Where(static r => !MessageScenarios.Lists.Contains(r)))
        {
            rows.Add(slug, scenario);
        }

        return rows;
    }

    [Theory(Timeout = 30_000), Trait("Lane", "integration")]
    [MemberData(nameof(Rows))]
    public Task Api_answer_matches_its_golden_file(string slug, string scenario) => ApiGoldenRows.Match(slug, scenario, TestContext.Current.CancellationToken);
}
