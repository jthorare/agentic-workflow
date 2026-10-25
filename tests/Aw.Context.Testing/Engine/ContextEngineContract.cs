using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Subgraph;
using Aw.Kernel.Testing;
using Xunit;

namespace Aw.Context.Testing.Engine;

/// <summary>
/// The contract every <see cref="IContextEngine"/> honours, run against the real engine (over the Context fakes) and
/// Sindri's test double: settings defaults and key paths, the profile-repo checks, and SND-CONTEXT-011 when no source
/// answers. A binding creates an engine whose sources (if any) all fail.
/// </summary>
/// <typeparam name="TEngine">The implementation.</typeparam>
public abstract class ContextEngineContract<TEngine>
    where TEngine : IContextEngine
{
    private static readonly ProfileRepoEntry[] _repos = [new("split-panes", "/repos/split-panes"), new("bad name", "/repos/bad")];

    /// <summary>An engine whose sources all fail (or that has none that can answer).</summary>
    /// <returns>The engine.</returns>
    protected abstract TEngine CreateDown();

    /// <summary>No section gives the defaults, even with a repo the engine can't use.</summary>
    [Fact]
    public void No_section_gives_the_defaults_and_refuses_nothing()
    {
        var settings = CreateDown().CheckSettings(section: null, _repos);

        Check(settings.IsSuccess && settings.Value.MinHits == 3, "no section gives the defaults");
    }

    /// <summary>A bad key, an unknown context.repos name and an unusable repo in scope are 001 with their key paths.</summary>
    [Fact]
    public void Bad_settings_and_repos_are_001_with_their_key_path()
    {
        var engine = CreateDown();

        Check(Code(engine, """{"deadline":"soon"}""") == ("SND-CONTEXT-001", "context.deadline:"), "a bad deadline");
        Check(Code(engine, """{"scrub":{"extraPatterns":[{"id":"acme-customer-id","pattern":"ACME-[0-9"}]}}""") == ("SND-CONTEXT-001", "context.scrub.extraPatterns[0].pattern:"),
            "an extra pattern that doesn't parse");
        Check(Code(engine, """{"repos":["nope"]}""") == ("SND-CONTEXT-001", "context.repos:"), "an unknown context.repos name");
        Check(Code(engine, """{"deadline":"4s"}""") == ("SND-CONTEXT-001", "context.repos:"), "an unusable repo name in scope");
        Check(Code(engine, """{"repos":["split-panes"]}""") == ("ok", ""), "an unusable repo out of scope is fine");
    }

    /// <summary>A query no source answers is SND-CONTEXT-011, never an empty answer.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_query_no_source_answers_is_011()
    {
        var scope = new ContextScope(Settings: null, [new ContextRepo(ContextRepoName.From("split-panes"), "/repos/split-panes")],
            new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

        var result = await CreateDown().Query(new ContextQuery("orientation"), scope, CancellationToken.None);

        Check(result.IsError && result.Error.Code == ContextErrors.AllSourcesUnavailable.Code, "no source answering is 011");
    }

    /// <summary>Without a map, the graph reads are SND-CONTEXT-030.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Graph_reads_without_a_map_are_030()
    {
        var scope = new ContextScope(Settings: null, [new ContextRepo(ContextRepoName.From("split-panes"), "/repos/split-panes")],
            new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));
        var engine = CreateDown();

        var codes = new[]
        {
            (await engine.ShowNode("feature:a", scope, CancellationToken.None)).Error.Code,
            (await engine.Neighbors("feature:a", depth: null, scope, CancellationToken.None)).Error.Code,
            (await engine.Path("feature:a", "feature:b", scope, CancellationToken.None)).Error.Code,
        };

        Check(codes.All(c => c == ContextErrors.NoMap.Code), "a graph read without a map is 030");
    }

    /// <summary>Without a map, a subgraph read and the scoping run's proposals reads and writes are SND-CONTEXT-030.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Subgraph_and_scope_proposals_without_a_map_are_030()
    {
        var scope = new ContextScope(Settings: null, [new ContextRepo(ContextRepoName.From("split-panes"), "/repos/split-panes")],
            new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));
        var engine = CreateDown();
        var proposal = new ScopeProposal("run-1", ContextRepoName.From("split-panes"), [new ProposedSurface("Split vertical")]);

        var codes = new[]
        {
            (await engine.Subgraph(new SubgraphQuery(["feature:a"], Depth: null, MapRead.Current), scope, CancellationToken.None)).Error.Code,
            (await engine.Proposals("run-1", scope, CancellationToken.None)).Error.Code,
            (await engine.Propose(proposal, scope, CancellationToken.None)).Error.Code,
        };

        Check(codes.All(c => c == ContextErrors.NoMap.Code), "a subgraph read, a proposals read and a propose without a map are 030");
    }

    /// <summary>A withdrawal (a run with no surfaces) on a store with no map takes nothing back: success, no draft, zero counts.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_withdraw_on_an_empty_store_succeeds_with_no_draft_and_zero_counts()
    {
        var scope = new ContextScope(Settings: null, [new ContextRepo(ContextRepoName.From("split-panes"), "/repos/split-panes")],
            new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

        var result = await CreateDown().Propose(new ScopeProposal("run-1", ContextRepoName.From("split-panes"), []), scope, CancellationToken.None);

        Check(result.IsSuccess && result.Value is { Draft: null, Added: 0, Existing: 0, Rejected: 0, Withdrawn: 0, Kept: 0 }, "a withdraw on an empty store is a success with no draft and zero counts");
    }

    /// <summary>An eval with no probe file named and none on disk is SND-CONTEXT-036, whatever the engine.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_eval_with_no_probe_file_is_036()
    {
        var run = await CreateDown().EvalProbes(file: null, static (_, _) => throw new ContractViolationException("no probe may run without a probe file"), CancellationToken.None);

        Check(run.IsError && run.Error.Code == ContextErrors.NoProbeFile.Code, "an eval with no probe file is 036");
    }

    private static (string Code, string Path) Code(TEngine engine, string json)
    {
        var checkedSettings = engine.CheckSettings(JsonDocument.Parse(json).RootElement.Clone(), _repos);
        return checkedSettings.IsSuccess ? ("ok", "") : (checkedSettings.Error.Code, checkedSettings.Error.Message[..(checkedSettings.Error.Message.IndexOf(':', StringComparison.Ordinal) + 1)]);
    }

    private static void Check(bool holds, string what)
    {
        if (!holds)
        {
            throw new ContractViolationException($"{typeof(TEngine).Name}: {what}");
        }
    }
}
