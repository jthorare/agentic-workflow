using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Features.BuildCodeIndex;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Build;

// The graph layer plugged into the build (spec §5.5, §5.7), over git, SQLite index files and a fake graph tool: a TypeScript repo gets graph symbols and
// graphify call edges that Callers and Callees return with their basis and a partial coverage, and a tool that is not usable never fails a build.
public sealed class BuildCodeIndexGraphTests
{
    private const string FileA = "export function f() {\n  return g();\n}\n";
    private const string FileB = "export function g() {\n  return 1;\n}\n";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly GraphSnapshot _graph = new(
        [new GraphNode("src_a_f", "src/a.ts", "f", 1, 0), new GraphNode("src_b_g", "src/b.ts", "g", 1, 0)],
        [new GraphEdge("src_a_f", "src_b_g", "calls", "EXTRACTED", 2)]);

    private static TempGitRepo Repo() => A.GitRepo().WithFile("src/a.ts", FileA).WithFile("src/b.ts", FileB).Build();

    private static async Task<BuildReport> Build(RealIndex index, CodeIndexRepo repo, CommitSha commit)
        => (await index.Service.Handle(new BuildCodeIndexRequest(repo, commit, new Progress<string>()), Token)).Value;

    private static async Task<SymbolRef> Find(RealIndex index, CodeIndexRepo repo, string name)
        => (await index.Facade.FindSymbols(RealIndex.ScopeOf(repo), new SymbolQuery(name, SymbolMatch.Exact, Repo: null), Token)).Value.Hits.Single().Ref;

    [Fact, Trait("Lane", "integration")]
    public async Task A_typescript_repo_gets_graph_symbols_and_callers_and_callees_from_graphify_with_partial_coverage()
    {
        using var git = Repo();
        var graph = new FakeGraphRunner { Graph = _graph };
        using var index = new RealIndex(graph: graph);
        var repo = git.AsRepo("web");

        var report = await Build(index, repo, git.Head);
        var g = await Find(index, repo, "g");
        var callers = (await index.Facade.Callers(RealIndex.ScopeOf(repo), g, CallDepth.One, includeImplementations: false, Token)).Value;
        var f = await Find(index, repo, "f");
        var callees = (await index.Facade.Callees(RealIndex.ScopeOf(repo), f, CallDepth.One, Token)).Value;

        var layer = report.Layers.Single(l => l.Layer == LayerName.Graph);
        Assert.Equal(LayerState.Ok, layer.State);
        Assert.Equal("2 symbols, 1 calls, 0 dropped", layer.Detail);
        var edge = Assert.Single(callers.Edges);
        Assert.Equal(("f", EdgeBasis.Graphify, Confidence.Certain, Coverage.Partial), (edge.Other!.Name, edge.Basis, edge.Confidence, edge.Coverage));
        Assert.Equal(Coverage.Partial, callers.Coverage);
        Assert.Equal("g", Assert.Single(callees.Edges).Other!.Name);
        Assert.Equal(1, graph.Runs);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_tool_that_is_not_usable_leaves_the_layer_unavailable_with_its_code_and_the_build_still_publishes()
    {
        using var git = Repo();
        using var index = new RealIndex(graph: new FakeGraphRunner { RunError = CodeIndexErrors.PinMismatch.With("version") });
        var repo = git.AsRepo("web");

        var report = await Build(index, repo, git.Head);

        var layer = report.Layers.Single(l => l.Layer == LayerName.Graph);
        Assert.Equal(BuildOutcome.Published, report.Outcome);
        Assert.Equal((LayerState.Unavailable, "SND-CODEIDX-009", "version"), (layer.State, layer.Code, layer.Detail));
        Assert.Equal(LayerState.Ok, report.Layers.Single(l => l.Layer == LayerName.Structure).State);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_failure_that_is_no_layer_code_reads_as_the_graph_code_failed()
    {
        using var git = Repo();
        using var index = new RealIndex(graph: new FakeGraphRunner { RunError = CodeIndexErrors.IndexDamaged.With("secret path /home/x") });
        var repo = git.AsRepo("web");

        var layer = (await Build(index, repo, git.Head)).Layers.Single(l => l.Layer == LayerName.Graph);

        Assert.Equal(("SND-CODEIDX-008", "failed"), (layer.Code, layer.Detail));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_later_build_with_no_usable_tool_serves_no_stale_graph_symbol_or_edge()
    {
        using var git = Repo();
        var tool = new FakeGraphRunner { Graph = _graph };
        using var index = new RealIndex(graph: tool);
        var repo = git.AsRepo("web");
        _ = await Build(index, repo, git.Head);
        var g = await Find(index, repo, "g");
        git.WriteFile("src/a.ts", FileA + "// edit\n");
        var second = git.Commit("edit");
        tool.RunError = CodeIndexErrors.GraphUnavailable.With("timeout");

        var report = await Build(index, repo, second);

        var callers = await index.Facade.Callers(RealIndex.ScopeOf(repo), g, CallDepth.One, includeImplementations: false, Token);
        var found = (await index.Facade.FindSymbols(RealIndex.ScopeOf(repo), new SymbolQuery("g", SymbolMatch.Exact, Repo: null), Token)).Value;
        Assert.Equal(("SND-CODEIDX-008", "timeout"), (report.Layers.Single(l => l.Layer == LayerName.Graph).Code, report.Layers.Single(l => l.Layer == LayerName.Graph).Detail));
        Assert.True(callers.IsError);
        Assert.Empty(found.Hits);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_disabled_graph_layer_is_disabled_and_the_tool_does_not_run()
    {
        var tool = new FakeGraphRunner { Graph = _graph };
        var harness = new BuildHarness { Graph = tool };
        _ = harness.Snapshot.WithFile(BuildHarness.First, "src/a.ts", FileA);
        var settings = CodeIndexSettings.Default with { Graph = CodeIndexSettings.Default.Graph with { Enabled = false } };
        var repo = new CodeIndexRepo(BuildHarness.Name, "/work/api", settings);

        var report = (await harness.Service.Handle(new BuildCodeIndexRequest(repo, BuildHarness.First, new Progress<string>()), Token)).Value;

        Assert.Equal(LayerState.Disabled, report.Layers.Single(l => l.Layer == LayerName.Graph).State);
        Assert.Equal(0, tool.Runs);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_graph_tool_gets_the_filtered_code_files_of_the_commit_and_the_layer_is_stamped_with_it()
    {
        var tool = new FakeGraphRunner { Graph = _graph };
        var harness = new BuildHarness { Graph = tool };
        _ = harness.Snapshot.WithFile(BuildHarness.First, "src/a.ts", FileA).WithFile(BuildHarness.First, "README.md", "# api");

        var report = await harness.Build(BuildHarness.First, cancellationToken: Token);

        var request = Assert.Single(tool.Requests);
        Assert.Equal(["src/a.ts"], request.Files.Select(f => f.Path), StringComparer.Ordinal);
        Assert.Equal(BuildHarness.First, request.Commit);
        var header = await harness.Header();
        Assert.Equal(BuildHarness.First.Value, header!.Layers.Single(l => l.Layer == LayerName.Graph).Stamp);
        Assert.Equal(LayerState.Ok, report.Layers.Single(l => l.Layer == LayerName.Graph).State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ProbeLayers_reports_the_graph_layer_buildable_only_when_it_was_unavailable_and_the_tool_now_checks_out()
    {
        var tool = new FakeGraphRunner { RunError = CodeIndexErrors.NoSandbox.With("none"), ProbeError = CodeIndexErrors.NoSandbox.With("none") };
        var harness = new BuildHarness { Graph = tool };
        _ = harness.Snapshot.WithFile(BuildHarness.First, "src/a.ts", FileA);
        _ = await harness.Build(BuildHarness.First, cancellationToken: Token);

        var still = (await harness.Facade.ProbeLayers(harness.Scope, Token)).Value.Single();
        tool.ProbeError = null;
        var now = (await harness.Facade.ProbeLayers(harness.Scope, Token)).Value.Single();

        Assert.Empty(still.Buildable);
        Assert.Equal([LayerName.Graph], now.Buildable);
    }
}
