using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// Not registered in ApiScenarios until the Linear hub's read routes land (the rows answer 404 until then).
/// Work → Linear, the strip, the issue sheet and New issue's lookups (Linear hub spec §3, §4.3, Ph3 1a; contract AGREED by api and ui,
/// ~/.agentic-workflow/digests/api-scripts/contract-2077-ph3-linear-reads.md): 'GET /linear/issues', 'GET /linear/issues/{key}',
/// 'GET /linear/lookups/{team}' and 'GET /linear/since'. Hand-written first: the routes don't exist yet. The api slot seeds what each
/// row's `// seeded by api:` note names.
/// </summary>
internal static class LinearScenarios
{
    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("linear-issues", "mine"), ("linear-issues", "teams-page2"), ("linear-issues", "filtered-empty"), ("linear-issues", "not-connected"),
        ("linear-issues", "feed-failing"), ("linear-issues", "rereading"), ("linear-issues", "session"),
        ("linear-issue", "full"), ("linear-issue", "removed"), ("linear-issue", "not-found"), ("linear-issue", "session"), ("linear-issue", "session-outside"),
        ("linear-lookups", "eng"), ("linear-lookups", "not-synced"), ("linear-lookups", "session-refused"),
        ("linear-since", "three"), ("linear-since", "empty"), ("linear-since", "session-refused"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        await ViewScenarios.CatchUp(host, ct);
        // seeded by api: a linear source with teams ENG and OPS, viewer Joi Example; ENG-12, ENG-31 and OPS-40 assigned to the viewer, ENG-57 and ENG-44 not;
        // ENG-12 linked to an item whose run has PR acme/api#2071 waiting on the owner, ENG-57 to a done item with acme/api#2080 approved.
        // 'teams-page2' is page 2 of 52 teams rows (after the first page's cursor); 'filtered-empty' filters by an unknown label id; 'not-connected' has no linear source;
        // 'feed-failing' a feed whose last tick ended SND-LINEAR-010 at 22:05; 'rereading' a ;f3 re-read at 412 of 1,000; 'session' is 'mine' read by a session of a repo mapped to ENG.
        // One issue: 'removed' is ENG-9 tombstoned (moved to OPS's team outside the profile); 'not-found' ENG-99, never stored; 'session-outside' OPS-40 read by that session.
        // Lookups: 'eng' after a lookups tick, 'not-synced' OPS before one. Since: 'three' after an assignment, a mention and a state move by Sam Lee; 'empty' at the newest marker.
        var human = ItemScenarios.Human(host);
        var session = ItemScenarios.Session("demo");
        var caller = scenario.StartsWith("session", StringComparison.Ordinal) ? session : human;
        var path = (slug, scenario) switch
        {
            ("linear-issues", "teams-page2") => "/sindri/linear/issues?group=teams&after=page2&limit=50",
            ("linear-issues", "filtered-empty") => "/sindri/linear/issues?group=mine&label=00000000-0000-4000-8000-0000000000ff",
            ("linear-issues", _) => "/sindri/linear/issues?group=mine",
            ("linear-issue", "removed") => "/sindri/linear/issues/ENG-9",
            ("linear-issue", "not-found") => "/sindri/linear/issues/ENG-99",
            ("linear-issue", "session-outside") => "/sindri/linear/issues/OPS-40",
            ("linear-issue", _) => "/sindri/linear/issues/ENG-12",
            ("linear-lookups", "not-synced") => "/sindri/linear/lookups/OPS",
            ("linear-lookups", _) => "/sindri/linear/lookups/ENG",
            ("linear-since", "empty") => "/sindri/linear/since?after=lin_000000000000003f",
            _ => "/sindri/linear/since",
        };
        return (await host.Http.Ask(Get(path, caller))).Body;
    }
}
