namespace Aw.Sindri.Core.Graph.Templates;

/// <summary>
/// Templates A, B and C of the Workflows page spec (§4.1, E7, E13). Each is a full graph with the fields a person must fill left out (the repo of an agent step), so its first draft shows
/// "N things left". The three name the connection 'github:default'; the start screen swaps in the connection the box has.
/// </summary>
public static class StockTemplates
{
    private static readonly (string Key, string Title, string Trigger, string Json)[] _sources =
    [
        (
            "A",
            "Review every PR",
            "github.pr.opened",
            """{"version":1,"name":"Review every PR","nodes":[ {"id":"pr","type":"connection_event","title":"A PR is opened","config":{"connection":"github:default","event":"github.pr.opened"}}, {"id":"review","type":"run_agent","title":"Review it","config":{"prompt":"Review PR {{trigger.number}} ({{trigger.title}}) in {{trigger.repo}}. Report what you found.","outputs":{"summary":"string"}}}, {"id":"tell","type":"notify","title":"Tell me","config":{"title":"PR review done","body":"{{review.summary}}"}} ],"edges":[ {"from":"pr","to":"review"}, {"from":"review","to":"tell"} ]}"""),
        (
            "B",
            "Plan a labelled issue",
            "github.issue.labeled",
            """{"version":1,"name":"Plan a labelled issue","nodes":[ {"id":"issue","type":"connection_event","title":"An issue is labelled","config":{"connection":"github:default","event":"github.issue.labeled"}}, {"id":"plan","type":"prompt","title":"Write the plan","config":{"prompt":"Write a short plan for the issue {{trigger.title}} ({{trigger.url}}).","output":{"type":"object","required":["plan"],"properties":{"plan":{"type":"string"}}}}}, {"id":"comment","type":"call_tool","title":"Comment on the issue","config":{"connection":"github:default","tool":"add_comment","arguments":{"repo":"{{trigger.repo}}","issue":"{{trigger.number}}","body":"{{plan.plan}}"}}} ],"edges":[ {"from":"issue","to":"plan"}, {"from":"plan","to":"comment"} ]}"""),
        (
            "C",
            "Watch main, open an issue on red",
            "schedule",
            """{"version":1,"name":"Watch main, open an issue on red","nodes":[ {"id":"nightly","type":"schedule","title":"Every night at 2:00","config":{"cron":"0 2 * * *","timezone":"@machine-timezone@"}}, {"id":"check","type":"run_agent","title":"Check main","config":{"prompt":"Check whether the latest CI run on the main branch is red. Report red as true or false, and a summary.","outputs":{"red":"boolean","summary":"string"}}}, {"id":"gate","type":"condition","title":"Is main red?","config":{"when":"check.red"}}, {"id":"open","type":"call_tool","title":"Open an issue","config":{"connection":"github:default","tool":"create_issue","arguments":{"title":"Main is red","body":"{{check.summary}}"}}} ],"edges":[ {"from":"nightly","to":"check"}, {"from":"check","to":"gate"}, {"from":"gate","output":"true","to":"open"} ]}"""),
    ];

    /// <summary>Gets the templates in the order the start screen shows them.</summary>
    public static IReadOnlyList<StockTemplate> All => [.. _sources.Select(Build)];

    /// <summary>Finds a template by id.</summary>
    /// <param name="key">'A', 'B' or 'C'.</param>
    /// <returns>The template, or null.</returns>
    public static StockTemplate? Find(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var source = _sources.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal));
        return source.Key is null ? null : Build(source);
    }

    // why: the same zone rule as the weekday-check template: this machine's zone, UTC when the OS names it in a form the cron reader doesn't know
    private static string MachineTimezone() => TimeZoneInfo.Local.HasIanaId ? TimeZoneInfo.Local.Id : "UTC";

    private static StockTemplate Build((string Key, string Title, string Trigger, string Json) source)
    {
        // why: the JSON is ours; a parse failure is a bug in the text above, not an expected error
        var parsed = GraphJson.Parse(source.Json.Replace("@machine-timezone@", MachineTimezone(), StringComparison.Ordinal));
        return parsed.IsSuccess
            ? new StockTemplate(source.Key, source.Title, source.Trigger, ["connection:github"], parsed.Value)
            : throw new InvalidOperationException($"template '{source.Key}' does not parse: {parsed.Error.Message}");
    }
}
