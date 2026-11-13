using System.Text.Json;

namespace Aw.Sindri.Core.Graph.Templates;

/// <summary>The starter templates. Each call builds fresh copies, so a caller may edit a spec without changing the next caller's.</summary>
public static class GraphTemplates
{
    /// <summary>The repo name a template carries when no real one is given: <see cref="All"/> and <see cref="Find(string)"/> are for listing and for the tests, not for making a workflow.</summary>
    public const string PlaceholderRepo = "your-repo";

    private const string RepoMarker = "@repo@";

    private static readonly (string Name, string Description, string Json)[] _sources =
    [
        (
            "weekday-check",
            "Every weekday at 7:00 ask the model a question and send a notification only when its answer is flagged.",
            """{"version":1,"name":"Weekday check","nodes":[ {"id":"weekdays","type":"schedule","title":"Every weekday at 7:00","config":{"cron":"0 7 * * 1-5","timezone":"@machine-timezone@"}}, {"id":"ci","type":"run_agent","title":"Read CI","config":{"repo":"@repo@","prompt":"Run `gh run list --branch main --limit 5` and report the runs, newest first: each one's name, status and conclusion, and the failed job when there is one.","outputs":{"runs":"string"}}}, {"id":"check","type":"prompt","title":"Check","config":{"prompt":"Here are the latest CI runs on main:\n{{ci.runs}}\n\nIs main's CI red this morning? Set flagged to true when it is, and summarize which job failed.","output":{"type":"object","required":["flagged","summary"],"properties":{"flagged":{"type":"boolean"},"summary":{"type":"string"}}}}}, {"id":"gate","type":"condition","title":"Flagged?","config":{"when":"check.flagged"}}, {"id":"tell","type":"notify","title":"Tell me","config":{"title":"Needs attention","body":"{{check.summary}}"}} ],"edges":[ {"from":"weekdays","to":"ci"}, {"from":"ci","to":"check"}, {"from":"check","to":"gate"}, {"from":"gate","output":"true","to":"tell"} ]}"""),
        (
            "daily-digest",
            "Every day at 9:00 ask the model for a summary and send it as a notification.",
            """{"version":1,"name":"Daily digest","nodes":[ {"id":"daily","type":"schedule","title":"Every day at 9:00","config":{"cron":"0 9 * * *","timezone":"@machine-timezone@"}}, {"id":"look","type":"run_agent","title":"Read the repo","config":{"repo":"@repo@","prompt":"Run `gh pr list --state open` and `gh run list --branch main --limit 10`, then report the open pull requests and the runs that failed.","outputs":{"report":"string"}}}, {"id":"digest","type":"prompt","title":"Write the digest","config":{"prompt":"Here is what the repo shows today:\n{{look.report}}\n\nWrite a short summary of what to look at today.","output":{"type":"object","required":["summary"],"properties":{"summary":{"type":"string"}}}}}, {"id":"tell","type":"notify","title":"Send it","config":{"title":"Daily digest","body":"{{digest.summary}}"}} ],"edges":[ {"from":"daily","to":"look"}, {"from":"look","to":"digest"}, {"from":"digest","to":"tell"} ]}"""),
        (
            "manual-notify",
            "A button you press: it sends one notification. The smallest workflow to start from.",
            """{"version":1,"name":"Manual notify","nodes":[ {"id":"start","type":"manual","title":"Run now","config":{}}, {"id":"tell","type":"notify","title":"Tell me","config":{"title":"Hello from a workflow"}} ],"edges":[ {"from":"start","to":"tell"} ]}"""),
        (
            "hello-sindri",
            "Four steps with no account, model or network: make a greeting in code, tell you, and add an item. The setup service makes this one for the first repo.",
            """{"version":1,"name":"hello-sindri","nodes":[ {"id":"start","type":"manual","title":"Start","config":{}}, {"id":"greet","type":"code","title":"Make a greeting","config":{"code":"return \"Hello from Sindri\";","timeout":"60s"}}, {"id":"tell","type":"notify","title":"Tell me","config":{"title":"hello-sindri","body":"{{greet.result}}"}}, {"id":"add_item","type":"create_item","title":"Add an item","config":{"title":"Hello from Sindri"}} ],"edges":[ {"from":"start","to":"greet"}, {"from":"greet","to":"tell"}, {"from":"tell","to":"add_item"} ]}"""),
    ];

    /// <summary>Every template, in the order they are listed.</summary>
    public static IReadOnlyList<GraphTemplate> All => [.. _sources.Select(static s => Build(s, PlaceholderRepo))];

    /// <summary>Finds a template by name. A template with an agent step gets the stand-in repo name <see cref="PlaceholderRepo"/>; use <see cref="Find(string, string?)"/> to name the real one.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The template, or null.</returns>
    public static GraphTemplate? Find(string name) => Find(name, PlaceholderRepo);

    /// <summary>Finds a template by name and fills in the repo of its agent step (#3509): the profile's first repo.</summary>
    /// <param name="name">The name.</param>
    /// <param name="firstRepo">The profile's first repo, or null when it has none.</param>
    /// <returns>The template; null when there is none by that name, or when it needs a repo and <paramref name="firstRepo"/> is null.</returns>
    public static GraphTemplate? Find(string name, string? firstRepo)
    {
        ArgumentNullException.ThrowIfNull(name);
        var source = _sources.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.Ordinal));
        return source.Name is null || (firstRepo is null && NeedsRepo(source.Json)) ? null : Build(source, firstRepo);
    }

    /// <summary>Whether the template reads data in a repo through an agent step, so it can't be made without one.</summary>
    /// <param name="name">The name.</param>
    /// <returns>True for weekday-check and daily-digest; false for the others and for a name that is no template.</returns>
    public static bool NeedsRepo(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _sources.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.Ordinal)) is { Name: not null } source && NeedsRepo(source.Json);
    }

    private static bool NeedsRepo(string json) => json.Contains(RepoMarker, StringComparison.Ordinal);

    // why: a starter that fires at 7:00 Pacific on any box is wrong for most owners; use this machine's zone, and UTC when the OS names it in a form the cron reader doesn't know
    private static string MachineTimezone() => TimeZoneInfo.Local.HasIanaId ? TimeZoneInfo.Local.Id : "UTC";

    private static GraphTemplate Build((string Name, string Description, string Json) source, string? repo)
    {
        // why: the JSON is ours and tested to parse; a failure here is a bug in the text above, not an expected error.
        var parsed = GraphJson.Parse(source.Json.Replace("@machine-timezone@", MachineTimezone(), StringComparison.Ordinal).Replace(RepoMarker, JsonEncodedText.Encode(repo ?? string.Empty).ToString(), StringComparison.Ordinal));
        return parsed.IsSuccess
            ? new GraphTemplate(source.Name, source.Description, parsed.Value)
            : throw new InvalidOperationException($"template '{source.Name}' does not parse: {parsed.Error.Message}");
    }
}
