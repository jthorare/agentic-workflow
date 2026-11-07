using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The Reviews tab's login save (PR review page spec §6.1, §9): 'POST /reviews/login' with a body {login}.
/// </summary>
internal static class ReviewLoginScenarios
{
    private const string Route = "/sindri/reviews/login";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("reviews-login", "saved"), ("reviews-login", "pending"), ("reviews-login", "bad-login"), ("reviews-login", "session"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        await ViewScenarios.CatchUp(host, ct);
        // 'saved' starts on the approved profile (no `review` block), so the login is the only waiting change and the save approves it in one click ({login, approved: true});
        // 'pending' first drifts the live profile (a second service, not approved), so the login is written but the profile stays waiting ({login, approved: false})
        if (scenario == "pending")
        {
            await ViewScenarios.Drift(host, GoldenHost.ProfileYaml, ct);
        }

        return scenario switch
        {
            "bad-login" => (await host.Http.Ask(Post(Route, """{"login":"-bad-"}""", ItemScenarios.Human(host)))).Body,
            "session" => (await host.Http.Ask(Post(Route, """{"login":"joi-example"}""", ItemScenarios.Session("demo")))).Body,
            _ => (await host.Http.Ask(Post(Route, """{"login":"joi-example"}""", ItemScenarios.Human(host)))).Body,
        };
    }
}
