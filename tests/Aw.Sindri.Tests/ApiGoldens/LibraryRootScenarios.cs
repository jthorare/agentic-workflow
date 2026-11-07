using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// Not registered in ApiScenarios until the api lane's Settings -> Library slice lands (the rows answer 404 until then).
/// Settings -> Library's three writes (PR review page spec §6.3, §16; contract 2026-11-06 "Settings -> Library roots"): 'POST /library/roots', 'DELETE /library/roots/{name}' and
/// 'POST /library/aw', every answer under the one slug library-roots. Hand-written by the ui lane against the agreed contract; the api lane's slice seeds what each row's
/// `// seeded:` note names and matches the goldens.
/// </summary>
internal static class LibraryRootScenarios
{
    private const string Roots = "/sindri/library/roots";
    private const string Aw = "/sindri/library/aw";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("library-roots", "added"), ("library-roots", "added-pending-others"), ("library-roots", "removed"), ("library-roots", "aw-off"),
        ("library-roots", "bad-name"), ("library-roots", "duplicate-name"), ("library-roots", "path-not-absolute"), ("library-roots", "path-too-wide"),
        ("library-roots", "too-many-vaults"), ("library-roots", "inside-another"), ("library-roots", "bad-deny"),
        ("library-roots", "remove-not-vault"), ("library-roots", "remove-unknown"), ("library-roots", "remove-repo-root"), ("library-roots", "remove-escaped-unknown"),
        ("library-roots", "session-refused"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        await ViewScenarios.CatchUp(host, ct);
        var human = ItemScenarios.Human(host);
        // seeded: 'added' starts on an approved profile with no other waiting change, so the save approves in one click ({approved: true, pendingCount: 0});
        // 'added-pending-others' starts with two other unapproved profile changes ({approved: false, pendingCount: 2}); 'removed' and 'aw-off' start like 'added' with the vault 'work' / the aw root on;
        // the 'duplicate-name', 'inside-another' and 'remove-*' rows seed the vault 'work' at ~/Documents/WorkVault; 'too-many-vaults' seeds ten vaults
        return scenario switch
        {
            "added" or "added-pending-others" => (await host.Http.Ask(Post(Roots, """{"name":"harbor-notes","path":"~/Documents/Harbor","deny":["Private/**"],"approve":true}""", human))).Body,
            "removed" => (await host.Http.Ask(Delete(Roots + "/work?approve=true", human))).Body,
            "aw-off" => (await host.Http.Ask(Post(Aw, """{"on":false,"approve":true}""", human))).Body,
            "bad-name" => (await host.Http.Ask(Post(Roots, """{"name":"Work Notes","path":"~/Documents/Notes","deny":[],"approve":true}""", human))).Body,
            "duplicate-name" => (await host.Http.Ask(Post(Roots, """{"name":"work","path":"~/Documents/Other","deny":[],"approve":true}""", human))).Body,
            "path-not-absolute" => (await host.Http.Ask(Post(Roots, """{"name":"notes","path":"Documents/Notes","deny":[],"approve":true}""", human))).Body,
            "path-too-wide" => (await host.Http.Ask(Post(Roots, """{"name":"notes","path":"~","deny":[],"approve":true}""", human))).Body,
            "too-many-vaults" => (await host.Http.Ask(Post(Roots, """{"name":"eleventh","path":"~/Documents/Eleventh","deny":[],"approve":true}""", human))).Body,
            "inside-another" => (await host.Http.Ask(Post(Roots, """{"name":"q4","path":"~/Documents/WorkVault/Q4","deny":[],"approve":true}""", human))).Body,
            "bad-deny" => (await host.Http.Ask(Post(Roots, """{"name":"notes","path":"~/Documents/Notes","deny":["/etc/**"],"approve":true}""", human))).Body,
            "remove-not-vault" => (await host.Http.Ask(Delete(Roots + "/aw?approve=true", human))).Body,
            "remove-unknown" => (await host.Http.Ask(Delete(Roots + "/old-notes?approve=true", human))).Body,
            "remove-repo-root" => (await host.Http.Ask(Delete(Roots + "/repo%3Aacme%2Fapi?approve=true", human))).Body,
            "remove-escaped-unknown" => (await host.Http.Ask(Delete(Roots + "/no%2Fsuch?approve=true", human))).Body,
            _ => (await host.Http.Ask(Post(Roots, """{"name":"harbor-notes","path":"~/Documents/Harbor","deny":[],"approve":true}""", ItemScenarios.Session("demo")))).Body,
        };
    }
}
