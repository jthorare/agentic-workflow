using System.Globalization;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// Registered in ApiScenarios' Reviews group (Ph5b).
/// The library sheet's read (PR review page spec §4.1, §6.3, §9): 'GET /library' in each state. Hand-written first (Ph5 red); each row runs over a fake of the library facts that holds what its name says.
/// </summary>
internal static class LibraryScenarios
{
    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("library", "ok"), ("library", "with-errors"), ("library", "near-misses-only"), ("library", "empty"), ("library", "capped"), ("library", "root-unreadable"), ("library", "session"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts the scenario's host: the item profile over a fake of the library's facts.</summary>
    public static Task<GoldenHost> Start(string scenario, CancellationToken ct) =>
        GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml, extra: services => _ = services.Replace(ServiceDescriptor.Singleton<ILibraryPageFacts>(new FakeLibraryPageFacts().With(Facts(scenario)))));

    private static LibraryPageFacts Facts(string scenario) => scenario switch
    {
        "ok" => new(Docs: 41, [], [], []),
        "with-errors" => new(Docs: 41,
        [
            Broken("booking-retry-tdd", "~/notes/design/booking-retry-tdd.md", ("SND-LIB-010", "line 4: a missing or bad front-matter key: status 'draft2' isn't one of the allowed values. Write the key as the table above says")),
            Broken("queue-sizing-spec", "~/.agentic-workflow/acme-api/specs/queue-sizing-spec.md", ("SND-LIB-012", "line 2: another doc in the scope has this doc id. Rename one of them")),
            Broken("export-plan", "~/notes/design/export-plan.md", ("SND-LIB-015", "line 31: a ref to an item the scope doesn't have: queue-sizing-spec#REQ-9. Fix the ref, or add the item"), ("SND-LIB-016", "line 5: an unknown parent: booking-retry. Use an id in the scope, or the full ref")),
        ], [], []),
        "near-misses-only" => new(Docs: 38, [],
        [
            Near("~/notes/design/queue-sizing.md", "line 2: a near miss: aw-doc is \"1\", not the number 1."),
            Near("~/notes/design/export-limits.md", "line 2: a near miss: aw-doc is 1.0, not the number 1."),
            Near("~/notes/design/retry-notes.md", "line 1: a near miss: the first line is blank, so the front matter isn't read."),
        ], []),
        "empty" => new(Docs: 0, [], [], []),
        // why: 250 docs with errors against the cap of 200, so the page lists the first 200 and says so
        "capped" => new(Docs: 300, [.. Enumerable.Range(1, 250).Select(n => Broken(Draft(n), $"~/notes/design/{Draft(n)}.md", ("SND-LIB-010", "line 3: a missing or bad front-matter key: kind is missing. Write the key as the table above says")))], [], []),
        "root-unreadable" => new(Docs: 33, [], [], [new ReviewUnreadableRoot("~/notes/vault", "permission denied")]),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "not a library scenario"),
    };

    private static string Draft(int n) => "draft-" + n.ToString("000", CultureInfo.InvariantCulture);

    // why: the golden's texts are "<path> <text>", the way the real adapter writes a finding
    private static LibraryBrokenDoc Broken(string doc, string path, params (string Rule, string Text)[] errors) =>
        new(doc, path, [.. errors.Select(e => new LibraryDocError(e.Rule, $"{path} {e.Text}"))]);

    private static LibraryNearMiss Near(string path, string text) =>
        new(path, "SND-LIB-019", $"{path} {text} Write `aw-doc: 1` unquoted in the front matter, and make `---` the file's first line");

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        await ViewScenarios.CatchUp(host, ct);
        // seeded in Ph5: the library feed per scenario. 'ok': 41 docs; 'with-errors': 41 docs, 3 broken (SND-LIB-010, 012, and 015 with 016); 'near-misses-only': 38 docs and three near misses
        // (`aw-doc: "1"`, `aw-doc: 1.0`, a blank first line); 'empty': no docs; 'capped': 300 docs, 250 with errors, the first 200 listed; 'root-unreadable': 33 docs and ~/notes/vault unreadable
        // the roots (Settings -> Library): 'ok' aw, vault work, a not-scanned vault inbox and two repo roots; 'with-errors' the same with 2 errors in work and 1 in repo:jthorare/agentic-workflow;
        // 'empty' aw turned off and two repo roots with no docs; 'root-unreadable' the vault notes unreadable ("permission denied"); 'session' is asked by an agent session and answers roots []
        return (await host.Http.Ask(Get("/sindri/library", string.Equals(scenario, "session", StringComparison.Ordinal) ? ItemScenarios.Session("demo") : ItemScenarios.Human(host)))).Body;
    }
}
