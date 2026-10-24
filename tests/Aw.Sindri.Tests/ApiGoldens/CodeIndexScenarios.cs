using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The code-index read routes' goldens (code-index spec §10, CI-10a) on a host whose facade is a <see cref="FakeCodeIndex"/> over the item profile's two repos:
/// 'demo' holds C# and TypeScript symbols and 'other' one more. The goldens carry the partial-coverage words and the uniform not-found a session gets for another repo.
/// </summary>
internal static class CodeIndexScenarios
{
    private static readonly CommitSha _commit = CommitSha.From("0123456789abcdef0123456789abcdef01234567");
    private static readonly CommitSha _head = CommitSha.From("fedcba9876543210fedcba9876543210fedcba98");
    private static readonly RepoName _demo = RepoName.From("demo");
    private static readonly RepoName _other = RepoName.From("other");
    internal static readonly SymbolRef Run = SymbolRef.From("demo:src/Worker.cs#Worker.Run");
    internal static readonly SymbolRef Stop = SymbolRef.From("demo:src/Worker.cs#Worker.Stop");
    internal static readonly SymbolRef Helper = SymbolRef.From("demo:src/Helper.cs#Helper.Assist");
    internal static readonly SymbolRef Go = SymbolRef.From("demo:src/tabs.ts#go");
    internal static readonly SymbolRef Secret = SymbolRef.From("other:src/Secret.cs#Secret.Keep");

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("code-index-status", "all-repos"), ("code-index-status", "one-repo"), ("code-index-status", "unknown-repo-013"), ("code-index-status", "session-own-repo"), ("code-index-status", "session-other-repo-013"),
        ("code-index-symbols", "exact"), ("code-index-symbols", "prefix"), ("code-index-symbols", "empty-012"), ("code-index-symbols", "bad-match-012"), ("code-index-symbols", "session-other-repo-none"), ("code-index-symbols", "session-fenced"),
        ("code-index-callers", "csharp-full"), ("code-index-callers", "csharp-partial-row"), ("code-index-callers", "typescript-none-found"), ("code-index-callers", "with-repo"), ("code-index-callers", "depth-9-012"),
        ("code-index-callers", "unknown-ref-011"), ("code-index-callers", "session-other-repo-013"), ("code-index-callers", "bad-ref-012"),
        ("status", StaleScenario),
        ("code-index-callees", "graphify-partial"), ("code-index-callees", "depth-2"), ("code-index-callees", "session-other-repo-013"), ("code-index-callers", "session-fenced"),
    ];

    /// <summary>GET /status with the 'demo' index a commit behind main: one stale code-index row in health.problems.</summary>
    internal const string StaleScenario = "code-index-stale";

    /// <summary>Whether the row belongs to this class.</summary>
    public static bool Handles(string slug, string scenario) => slug.StartsWith("code-index-", StringComparison.Ordinal) || (slug, scenario) is ("status", StaleScenario);

    /// <summary>The facade the goldens read.</summary>
    /// <returns>A fake with the 'demo' and 'other' indexes.</returns>
    internal static FakeCodeIndex Index()
    {
        var run = Hit(Run, "src/Worker.cs", "Worker.Run", 10, 20);
        var stop = Hit(Stop, "src/Worker.cs", "Worker.Stop", 22, 30);
        var helper = Hit(Helper, "src/Helper.cs", "Helper.Assist", 5, 9);
        return new FakeCodeIndex()
            .WithIndex(_demo, _commit, run, stop, helper, Hit(Go, "src/tabs.ts", "go", 3, 3))
            .WithIndex(_other, _commit, Hit(Secret, "src/Secret.cs", "Secret.Keep", 1, 4))
            .WithEdges(_demo, Run, CallDirection.Callers, unresolved: 0, new CallEdge(1, stop, "Run", EdgeKind.Calls, EdgeBasis.Syntax, Confidence.Certain, Line: 25, Candidates: 1, Coverage.Full))
            .WithEdges(_demo, Stop, CallDirection.Callers, unresolved: 0,
                new CallEdge(1, run, "Stop", EdgeKind.Calls, EdgeBasis.Syntax, Confidence.Certain, Line: 15, Candidates: 1, Coverage.Full),
                new CallEdge(1, helper, "Stop", EdgeKind.Calls, EdgeBasis.Graphify, Confidence.Likely, Line: 7, Candidates: 2, Coverage.Partial))
            .WithEdges(_demo, Run, CallDirection.Callees, unresolved: 2,
                new CallEdge(1, helper, "Assist", EdgeKind.Calls, EdgeBasis.Syntax, Confidence.Certain, Line: 12, Candidates: 1, Coverage.Full),
                new CallEdge(1, Other: null, "Log", EdgeKind.Calls, EdgeBasis.Graphify, Confidence.Likely, Line: 14, Candidates: 0, Coverage.Partial));
    }

    /// <summary>Starts the golden host with <see cref="Index"/> as the facade and the item profile approved.</summary>
    /// <param name="ct">Cancels the start.</param>
    /// <param name="demoBehindMain">Whether the live head of 'demo' is past its index (the health row of the stale scenario); the status goldens keep it current.</param>
    public static async Task<GoldenHost> Start(CancellationToken ct, bool demoBehindMain = false)
    {
        var host = await GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml, extra: services =>
        {
            _ = services.Replace(ServiceDescriptor.Singleton<ICodeIndex>(Index()));
            if (demoBehindMain)
            {
                _ = services.Replace(ServiceDescriptor.Singleton<IRepoHeads>(new FakeRepoHeads().With(_demo, _head, onDefaultBranch: true)));
            }
        });
        await ViewScenarios.Approve(host, ct);
        return host;
    }

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (scenario == StaleScenario)
        {
            return (await host.Http.Ask(Get("/sindri/status", ItemScenarios.Human(host)))).Body;
        }

        var session = scenario.StartsWith("session-", StringComparison.Ordinal);
        var headers = session ? ItemScenarios.Session("demo") : ItemScenarios.Human(host);
        return (await host.Http.Ask(Get(Route(slug, scenario), headers))).Body;
    }

    private static string Route(string slug, string scenario) => (slug, scenario) switch
    {
        ("code-index-status", "all-repos") => "/sindri/code-index/status",
        ("code-index-status", "one-repo") => "/sindri/code-index/status?repo=other",
        ("code-index-status", "unknown-repo-013") => "/sindri/code-index/status?repo=nope",
        ("code-index-status", "session-own-repo") => "/sindri/code-index/status",
        ("code-index-status", "session-other-repo-013") => "/sindri/code-index/status?repo=other",
        ("code-index-symbols", "exact") => "/sindri/code-index/symbols?query=Worker.Run",
        ("code-index-symbols", "prefix") => "/sindri/code-index/symbols?query=Worker.&match=prefix",
        ("code-index-symbols", "empty-012") => "/sindri/code-index/symbols?query=",
        ("code-index-symbols", "bad-match-012") => "/sindri/code-index/symbols?query=Run&match=like",
        ("code-index-symbols", "session-fenced") => "/sindri/code-index/symbols?query=Worker.Run",
        ("code-index-symbols", "session-other-repo-none") => "/sindri/code-index/symbols?query=Secret.Keep",
        ("code-index-callers", "csharp-full") => Callers(Run),
        ("code-index-callers", "csharp-partial-row") => Callers(Stop),
        ("code-index-callers", "typescript-none-found") => Callers(Go),
        ("code-index-callers", "with-repo") => "/sindri/code-index/callers?ref=" + Uri.EscapeDataString("src/Worker.cs#Worker.Run") + "&repo=demo",
        ("code-index-callers", "depth-9-012") => Callers(Run) + "&depth=9",
        ("code-index-callers", "unknown-ref-011") => Callers(SymbolRef.From("demo:src/Worker.cs#Worker.Nope")),
        ("code-index-callers", "session-other-repo-013") => Callers(Secret),
        ("code-index-callers", "session-fenced") => Callers(Run),
        ("code-index-callers", "bad-ref-012") => "/sindri/code-index/callers?ref=" + Uri.EscapeDataString("not a ref"),
        ("code-index-callees", "graphify-partial") => Callees(Run),
        ("code-index-callees", "depth-2") => Callees(Run) + "&depth=2",
        ("code-index-callees", "session-other-repo-013") => Callees(Secret),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "no such code-index scenario"),
    };

    private static string Callers(SymbolRef symbol) => "/sindri/code-index/callers?ref=" + Uri.EscapeDataString(symbol.Value);

    private static string Callees(SymbolRef symbol) => "/sindri/code-index/callees?ref=" + Uri.EscapeDataString(symbol.Value);

    private static SymbolHit Hit(SymbolRef symbol, string file, string name, int start, int end) => new(symbol, SymbolKind.Method, name, file, start, end, "void " + name + "()", Exported: true);
}
