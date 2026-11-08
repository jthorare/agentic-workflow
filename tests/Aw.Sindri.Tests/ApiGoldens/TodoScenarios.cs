namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// Not registered in ApiScenarios until the todo routes land (todo spec §10, Ph1a, #2264). The golden is hand-written first so the ui lane's Home panel (Ph2)
/// builds against it today: 'GET /sindri/todos?state=all' on a made-up list of five open todos (a past, a today and a later due date and one with none; every link kind;
/// one added by an agent, one by a piped CLI call), one done and one removed. The Ph1a implementation seeds the same events and regenerates the file with AW_GOLDEN_UPDATE=1, diff reviewed.
/// </summary>
internal static class TodoScenarios
{
    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } = [("todos", "list")];
}
