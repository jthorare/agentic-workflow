using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// Not registered in ApiScenarios until the Ph5 route lands (the rows answer 404 until then).
/// The Reviews tab's login save (PR review page spec §6.1, §9): 'POST /reviews/login' with a body {login}. Hand-written first (Ph5 red): the route doesn't exist yet, so every row
/// answers 404 against its golden. The Ph5 implementation seeds what each row's `// seeded in Ph5:` note names.
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
        // seeded in Ph5: 'saved' starts on a profile with no `review` block, so the login is the only waiting change and the save is approved in one click ({login, approved: true});
        // 'pending' starts with another unapproved profile change, so the login is written but stays waiting ({login, approved: false})
        return scenario switch
        {
            "bad-login" => (await host.Http.Ask(Post(Route, """{"login":"-bad-"}""", ItemScenarios.Human(host)))).Body,
            "session" => (await host.Http.Ask(Post(Route, """{"login":"joi-example"}""", ItemScenarios.Session("demo")))).Body,
            _ => (await host.Http.Ask(Post(Route, """{"login":"joi-example"}""", ItemScenarios.Human(host)))).Body,
        };
    }
}
