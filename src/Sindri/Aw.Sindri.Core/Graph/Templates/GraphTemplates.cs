namespace Aw.Sindri.Core.Graph.Templates;

/// <summary>The starter templates. Each call builds fresh copies, so a caller may edit a spec without changing the next caller's.</summary>
public static partial class GraphTemplates
{
    private static readonly (string Name, string Description, string Json)[] _sources =
    [
        (
            "weekday-check",
            "Every weekday at 7:00 ask the model a question and send a notification only when its answer is flagged.",
            """{"version":1,"name":"Weekday check","nodes":[ {"id":"weekdays","type":"schedule","title":"Every weekday at 7:00","config":{"cron":"0 7 * * 1-5","timezone":"@machine-timezone@"}}, {"id":"check","type":"prompt","title":"Check","config":{"prompt":"Today is {{weekdays.scheduledFor}}. Is today a Friday? Set flagged to true when it is, and set summary to: Friday: review your open pull requests before the weekend.","output":{"type":"object","required":["flagged","summary"],"properties":{"flagged":{"type":"boolean"},"summary":{"type":"string"}}}}}, {"id":"gate","type":"condition","title":"Flagged?","config":{"when":"check.flagged"}}, {"id":"tell","type":"notify","title":"Tell me","config":{"title":"Needs attention","body":"{{check.summary}}"}} ],"edges":[ {"from":"weekdays","to":"check"}, {"from":"check","to":"gate"}, {"from":"gate","output":"true","to":"tell"} ]}"""),
        (
            "daily-digest",
            "Every day at 9:00 ask the model for a summary and send it as a notification.",
            """{"version":1,"name":"Daily digest","nodes":[ {"id":"daily","type":"schedule","title":"Every day at 9:00","config":{"cron":"0 9 * * *","timezone":"@machine-timezone@"}}, {"id":"digest","type":"prompt","title":"Write the digest","config":{"prompt":"Today is {{daily.scheduledFor}}. Write one short line for the start of the day: the weekday, and one thing worth doing on that day of the week (for example Monday: plan the week, Friday: close open reviews).","output":{"type":"object","required":["summary"],"properties":{"summary":{"type":"string"}}}}}, {"id":"tell","type":"notify","title":"Send it","config":{"title":"Daily digest","body":"{{digest.summary}}"}} ],"edges":[ {"from":"daily","to":"digest"}, {"from":"digest","to":"tell"} ]}"""),
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
    public static IReadOnlyList<GraphTemplate> All => [.. _sources.Select(Build)];

    /// <summary>Finds a template by name.</summary>
    /// <param name="name">The name.</param>
    /// <param name="ci">The repo's CI to read, or null for the plain template. Only weekday-check and daily-digest use it.</param>
    /// <returns>The template, or null.</returns>
    public static GraphTemplate? Find(string name, GitHubCiSource? ci = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (ci is not null && CiTemplate(name, ci) is { } withCi)
        {
            return withCi;
        }

        var source = _sources.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.Ordinal));
        return source.Name is null ? null : Build(source);
    }

    // why: a starter that fires at 7:00 Pacific on any box is wrong for most owners; use this machine's zone, and UTC when the OS names it in a form the cron reader doesn't know
    private static string MachineTimezone() => TimeZoneInfo.Local.HasIanaId ? TimeZoneInfo.Local.Id : "UTC";

    private static GraphTemplate Build((string Name, string Description, string Json) source)
    {
        // why: the JSON is ours and tested to parse; a failure here is a bug in the text above, not an expected error.
        var parsed = GraphJson.Parse(source.Json.Replace("@machine-timezone@", MachineTimezone(), StringComparison.Ordinal));
        return parsed.IsSuccess
            ? new GraphTemplate(source.Name, source.Description, parsed.Value)
            : throw new InvalidOperationException($"template '{source.Name}' does not parse: {parsed.Error.Message}");
    }
}
