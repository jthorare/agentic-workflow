using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// Not registered in ApiScenarios until the Linear hub's read routes land (the rows answer 404 until then). 'GET /linear/issues' has landed: its rows are <see cref="LinearIssueScenarios"/>.
/// Work → Linear, the strip, the issue sheet and New issue's lookups (Linear hub spec §3, §4.3, Ph3 1a; contract AGREED by api and ui,
/// ~/.agentic-workflow/digests/api-scripts/contract-2077-ph3-linear-reads.md): 'GET /linear/issues', 'GET /linear/issues/{key}',
/// 'GET /linear/lookups/{team}' and 'GET /linear/since' (served now, see <see cref="LinearReadScenarios"/>). Hand-written first: the routes don't exist yet. The api slot seeds what each
/// row's `// seeded by api:` note names.
/// The 1b writes follow the same way (contract ~/.agentic-workflow/digests/api-scripts/contract-2077-1b-linear-writes.md, AGREED by api and ui): the edit, comment and create
/// routes and Settings, Connections, Linear (`linear-change`, `linear-comment`, `linear-create`, `linear-connection`, `linear-connection-teams`, `linear-connect`, `linear-disconnect-write`),
/// plus `write` on 'GET /linear/issues/{key}'. Their rows send the request body named in <c language="text">WriteRequest</c>.
/// </summary>
internal static class LinearScenarios
{
    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("linear-comment", "applied"), ("linear-comment", "applied-slow"), ("linear-comment", "unknown"), ("linear-comment", "session-refused"),
        ("linear-create", "applied"), ("linear-create", "unknown"), ("linear-create", "bad-value"), ("linear-create", "session-refused"),
        ("linear-connection", "connected"), ("linear-connection", "not-connected"), ("linear-connection", "read-only"), ("linear-connection", "scope-unknown"),
        ("linear-connection", "pending"), ("linear-connection", "session-refused"),
        ("linear-connection-teams", "two-teams"), ("linear-connection-teams", "refused"), ("linear-connection-teams", "unreachable"),
        ("linear-connect", "read-saved-approved"), ("linear-connect", "read-saved-waiting"), ("linear-connect", "write-saved"), ("linear-connect", "write-scope-unknown"),
        ("linear-connect", "same-as-read"), ("linear-connect", "no-keychain"), ("linear-connect", "no-teams"), ("linear-connect", "session-refused"),
        ("linear-disconnect-write", "removed"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        await ViewScenarios.CatchUp(host, ct);
        // the issue list, the sheet, the lookups and the strip are served (LinearIssueScenarios, LinearReadScenarios); what waits here are the 1b writes still to come (the edit's are LinearWriteScenarios)
        var human = ItemScenarios.Human(host);
        var session = ItemScenarios.Session("demo");
        var caller = scenario.StartsWith("session", StringComparison.Ordinal) ? session : human;
        if (WriteRequest(slug, scenario, caller) is { } write)
        {
            return (await host.Http.Ask(write)).Body;
        }

        var path = (slug, scenario) switch
        {
            ("linear-issue", _) => "/sindri/linear/issues/ENG-12",
            ("linear-connection", _) => "/sindri/linear/connection",
            _ => throw new ArgumentOutOfRangeException(nameof(slug), slug, "no such linear scenario"),
        };
        return (await host.Http.Ask(Get(path, caller))).Body;
    }

    // Connection: 'connected' has both keys resolved at 09:58; 'pending' has a live profile that differs from the approved one; 'same-as-read' sends the read key as the write key;
    // 'no-keychain' has none; 'no-teams' saves a read key with teams: [].
    private static HttpRequestMessage? WriteRequest(string slug, string scenario, (string Name, string Value)[] caller)
    {
        const string clientId = "5d0a4f6e-3b1c-4c0e-9a57-2f1d8c9b7e01";
        return slug switch
        {
            "linear-comment" => Post("/sindri/linear/issues/ENG-12/comments", $$"""{"clientId":"{{clientId}}","body":"Budget resets at midnight UTC: agreed."}""", caller),
            "linear-create" => Post("/sindri/linear/issues",
                scenario == "bad-value"
                    ? $$"""{"clientId":"{{clientId}}","team":"ENG","title":"Document the retry budget","assigneeId":"00000000-0000-4000-8000-0000000000ff"}"""
                    : $$"""{"clientId":"{{clientId}}","team":"ENG","title":"Document the retry budget"}""",
                caller),
            "linear-connection-teams" => Post("/sindri/linear/connection/teams", """{"key":"lin_api_read"}""", caller),
            "linear-connect" => Post("/sindri/linear/connection", scenario switch
            {
                "write-saved" or "write-scope-unknown" or "no-keychain" => """{"kind":"write","key":"lin_api_write"}""",
                "same-as-read" => """{"kind":"write","key":"lin_api_read"}""",
                "no-teams" => """{"kind":"read","key":"lin_api_read","teams":[]}""",
                _ => """{"kind":"read","key":"lin_api_read","teams":["ENG","OPS"]}""",
            }, caller),
            "linear-disconnect-write" => Delete("/sindri/linear/connection/write", caller),
            _ => null,
        };
    }
}
