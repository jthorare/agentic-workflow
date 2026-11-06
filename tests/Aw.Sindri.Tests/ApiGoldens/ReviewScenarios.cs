using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The Reviews page's goldens (PR review page spec §6.1-6.3, §9, §16): 'GET /reviews' in each list state and 'GET /reviews/{owner}/{repo}/{number}' in each page state. Hand-written first
/// (Ph5 red): the routes don't exist yet, so every row answers 404 against its golden. The Ph5 implementation seeds what each row's `// seeded in Ph5:` note names.
/// </summary>
internal static class ReviewScenarios
{
    private const string Pr = "/sindri/reviews/acme/api/2071";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("reviews", "some"), ("reviews", "empty"), ("reviews", "no-login"), ("reviews", "github-failing"), ("reviews", "before-reviewers"), ("reviews", "library-unreadable"),
        ("reviews-pr", "full"), ("reviews-pr", "empty-panels"), ("reviews-pr", "panel-errors"), ("reviews-pr", "moving"), ("reviews-pr", "offline-cached"),
        ("reviews-pr", "own-pr"), ("reviews-pr", "not-synced"), ("reviews-pr", "no-login"), ("reviews-pr", "not-found"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts the scenario's host on the item profile.</summary>
    public static Task<GoldenHost> Start(CancellationToken ct) =>
        // seeded in Ph5: the profile's `review` block per scenario (login joi-example, none for 'no-login'; projects[0] 'sindri' with the panel order and required checks) and `context.sources.github.repos` [acme/api, acme/web] (not acme/other)
        GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml);

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        await ViewScenarios.CatchUp(host, ct);
        // seeded in Ph5: list rows ('some': acme/api#2071 requested, acme/web#880 reviewed with 2 new commits, acme/api#2064 and draft acme/web#875 by joi-example; 'empty': none;
        // 'github-failing': the last rows plus a failing github freshness; 'before-reviewers': PR records without reviewers), the library (41 docs, 3 with errors; 'library-unreadable': 33 docs and one unreadable root)
        // and, for 'reviews-pr', the PR record, forge fakes for Files, CI runs, threads and evidence ('moving', 'offline-cached', 'own-pr' variants), all at the epoch clock
        var path = slug == "reviews" ? "/sindri/reviews" : scenario switch
        {
            "not-synced" => "/sindri/reviews/acme/other/12",
            "not-found" => "/sindri/reviews/acme/api/9999",
            _ => Pr,
        };
        return (await host.Http.Ask(Get(path, ItemScenarios.Human(host)))).Body;
    }
}
