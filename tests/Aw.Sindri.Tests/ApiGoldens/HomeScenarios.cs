using Aw.Kernel.Testing;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.HomeRoutes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The Home routes' goldens (home spec §8, S0): 'GET /sindri/home' over the §4.2 example rows of tests/fixtures/home/rows.json, an empty view, a page after a cursor and the snoozed read,
/// a session of repo demo that sees no row of repo other, 'GET /sindri/home/rows/{id}' for a percent-encoded id, an unknown id and a repo-less row read as a session,
/// 'GET /sindri/home/done' with a row done inside the 7 days and one outside, and 'GET /sindri/home/rank/{id}' for the meta row and an unknown id. The view is an in-memory one the host serves, so the goldens do not depend on a projector.
/// </summary>
internal static class HomeScenarios
{
    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("home", "fixture"), ("home", "empty"), ("home", "after-cursor"), ("home", "snoozed"),
        ("home", "session-repo"),
        ("home-rows-id", "row-by-id-encoded"), ("home-rows-id", "unknown-row-404"), ("home-rows-id", "session-dm-no-body"),
        ("home-done", "done-list"),
        ("home-rank-id", "rank-explain"), ("home-rank-id", "unknown-row-404"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts the scenario's host: the item profile, serving the scenario's view.</summary>
    /// <param name="scenario">The scenario.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The started host.</returns>
    public static Task<GoldenHost> Start(string scenario, CancellationToken ct)
    {
        var view = scenario == "empty" ? new InMemoryHomeView() : HomeFixture.View();
        if (scenario == "snoozed")
        {
            view.Put(HomeFixture.Row("gh:review:jthorare/agentic-workflow#3200", position: 7, state: HomeState.Snoozed));
        }

        if (scenario == "session-repo")
        {
            view.Put(HomeFixture.Row("gh:review:jthorare/other#3300", position: 7, repo: "other"));
        }

        if (scenario == "done-list")
        {
            view.Put(HomeFixture.Row("gh:review:jthorare/agentic-workflow#3210", position: 8, state: HomeState.Done));   // done at 2026-11-12T09:00, after the host clock's epoch: kept
            view.Put(HomeFixture.Row("gh:review:jthorare/agentic-workflow#3211", position: 9, state: HomeState.Done) with { DoneAt = KnownInstants.Epoch.AddDays(-30) });   // past the 7 days: left out
        }

        return GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml, extra: services =>
        {
            _ = services.RemoveAll<IHomeViewReader>();
            _ = services.AddSingleton<IHomeViewReader>(view);
        });
    }

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        await ViewScenarios.CatchUp(host, ct);
        var human = ItemScenarios.Human(host);
        var request = (slug, scenario) switch
        {
            ("home", "after-cursor") => Get($"/sindri/home?after={Uri.EscapeDataString(HomeFixture.Ready3099)}&limit=2", human),
            ("home", "snoozed") => Get("/sindri/home?snoozed=1", human),
            ("home", "session-repo") => Get("/sindri/home", ItemScenarios.Session("demo")),
            ("home", _) => Get("/sindri/home", human),
            ("home-done", _) => Get("/sindri/home/done", human),
            ("home-rank-id", "rank-explain") => Get($"/sindri/home/rank/{HomeRowId.From(HomeFixture.Meta).PathSegment()}", human),
            ("home-rank-id", _) => Get($"/sindri/home/rank/{HomeRowId.From("gh:review:jthorare/agentic-workflow#999").PathSegment()}", human),
            (_, "row-by-id-encoded") => Get($"/sindri/home/rows/{HomeRowId.From(HomeFixture.Review).PathSegment()}", human),
            (_, "unknown-row-404") => Get($"/sindri/home/rows/{HomeRowId.From("gh:review:jthorare/agentic-workflow#999").PathSegment()}", human),
            _ => Get($"/sindri/home/rows/{HomeRowId.From(HomeFixture.Dm).PathSegment()}", ItemScenarios.Session("demo")),
        };
        return (await host.Http.Ask(request)).Body;
    }
}
