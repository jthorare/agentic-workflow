using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// Not registered in ApiScenarios until the library route lands (Ph5b); its rows wait in ApiGoldenTests._pendingGoldenFolders.
/// The library sheet's read (PR review page spec §4.1, §6.3, §9): 'GET /library' in each state. Hand-written first (Ph5 red): the route doesn't exist yet, so every row answers 404
/// against its golden. The Ph5 implementation seeds what each row's `// seeded in Ph5:` note names.
/// </summary>
internal static class LibraryScenarios
{
    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("library", "ok"), ("library", "with-errors"), ("library", "near-misses-only"), ("library", "empty"), ("library", "capped"), ("library", "root-unreadable"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        await ViewScenarios.CatchUp(host, ct);
        // seeded in Ph5: the library feed per scenario. 'ok': 41 docs; 'with-errors': 41 docs, 3 broken (SND-LIB-010, 012, and 015 with 016); 'near-misses-only': 38 docs and three near misses
        // (`aw-doc: "1"`, `aw-doc: 1.0`, a blank first line); 'empty': no docs; 'capped': 300 docs, 250 with errors, the first 200 listed; 'root-unreadable': 33 docs and ~/notes/vault unreadable
        return (await host.Http.Ask(Get("/sindri/library", ItemScenarios.Human(host)))).Body;
    }
}
