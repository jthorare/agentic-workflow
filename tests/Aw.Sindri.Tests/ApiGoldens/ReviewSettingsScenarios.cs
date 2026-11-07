using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// Not registered in ApiScenarios until the Ph9 route lands (the rows answer 404 until then).
/// The project sheet's Review settings (PR review page spec §7, Ph9; contract PROPOSED by ui): 'GET /reviews/settings?project=' and 'PUT /reviews/settings'. Hand-written first: the route
/// doesn't exist yet. The Ph9 implementation seeds what each row's `// seeded in Ph9:` note names.
/// </summary>
internal static class ReviewSettingsScenarios
{
    private const string Route = "/sindri/reviews/settings";
    private const string Project = "prj_0000000000000000000000000H";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("review-settings", "default-only"), ("review-settings", "extends-sindri"), ("review-settings", "shared-entry"), ("review-settings", "session"),
        ("review-settings-save", "approved"), ("review-settings-save", "pending-others"), ("review-settings-save", "bad-panels"), ("review-settings-save", "bad-group-globs"),
        ("review-settings-save", "bad-extends"), ("review-settings-save", "bad-repo"), ("review-settings-save", "bad-settings-name"), ("review-settings-save", "unknown-project"),
        ("review-settings-save", "session-refused"), ("review-settings-save", "stale-hash"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        await ViewScenarios.CatchUp(host, ct);
        // seeded in Ph9: 'default-only' is the attached project with a review.projects row and no entry; 'extends-sindri' a `sindri` entry that extends default and sets four keys;
        // 'shared-entry' the same entry also used by prj_…K and with two checks; 'session' is 'extends-sindri' read by a session. Saves: 'approved' on a profile with no other waiting change,
        // 'pending-others' with two waiting; each refusal sends the one bad key its name says, and 'session-refused' is a session's PUT
        var human = ItemScenarios.Human(host);
        if (string.Equals(slug, "review-settings", StringComparison.Ordinal))
        {
            var caller = string.Equals(scenario, "session", StringComparison.Ordinal) ? ItemScenarios.Session("demo") : human;
            return (await host.Http.Ask(Get($"{Route}?project={Project}", caller))).Body;
        }

        return (await host.Http.Ask(Put(Route, SaveBody(scenario), string.Equals(scenario, "session-refused", StringComparison.Ordinal) ? ItemScenarios.Session("demo") : human))).Body;
    }

    private static string SaveBody(string scenario) => scenario switch
    {
        "bad-panels" => Save(Project, "jthorare/agentic-workflow", "sindri", """{"panels":["ci","ci"]}"""),
        "bad-group-globs" => Save(Project, "jthorare/agentic-workflow", "sindri", """{"groups":[{"name":"A","globs":["a/**"]},{"name":"B","globs":["b/**"]},{"name":"C","globs":["["]}]}"""),
        "bad-extends" => Save(Project, "jthorare/agentic-workflow", "sindri", """{"extends":"sindri"}"""),
        "bad-repo" => Save(Project, "agentic-workflow", "sindri", "{}"),
        "bad-settings-name" => Save(Project, "jthorare/agentic-workflow", "my settings", "{}"),
        "unknown-project" => Save("prj_00000000000000000000000099", "jthorare/agentic-workflow", "sindri", "{}"),
        "stale-hash" => Save(Project, "jthorare/agentic-workflow", "sindri", "{}", ifHash: "sha256:00"),
        _ => Save(Project, "jthorare/agentic-workflow", "sindri", """{"panels":["requiredChecks","ci"]}"""),
    };

    private static string Save(string project, string repo, string settings, string entry, string ifHash = "sha256:4f1c0b7e9a52d3") =>
        $$"""{"project":"{{project}}","repos":["{{repo}}"],"settings":"{{settings}}","entry":{{entry}},"approve":true,"ifHash":"{{ifHash}}"}""";
}
