using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Gate;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks;

/// <summary>'gate.pr.base-is-default' (PB spec §10.1, BD4): passes only while the PR's base is the repo's default branch.</summary>
[Lane(TestLane.Unit)]
public sealed class PrBaseIsDefaultGateTests
{
    private const string Workspace = "/work/pr";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static GateContext Of(string? pr, string? workspace = Workspace, string? prState = null)
    {
        var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var facts = new Dictionary<FactKey, JsonElement>();
        if (pr is not null)
        {
            facts[FactKey.From("fact:pr")] = Json(pr);
        }

        if (workspace is not null)
        {
            facts[FactKey.From("fact:workspace")] = Json($"\"{workspace}\"");
        }

        if (prState is not null)
        {
            facts[FactKey.From("fact:prState")] = Json(prState);
        }

        return new GateContext(BlockId.From("gate.pr.base-is-default"), arguments, facts, Threshold: null);
    }

    private static PrBaseIsDefaultGate Gate(string? defaultBranch = "main") =>
        new(defaultBranch is null ? new FakeDefaultBranch() : new FakeDefaultBranch().With(Workspace, defaultBranch));

    [Fact]
    public async Task A_base_that_is_the_default_branch_passes()
    {
        var result = await Gate("main").Evaluate(Of("""{"base":{"ref":"main"}}"""), Token);

        Assert.Equal(GateVerdict.Pass, result.Verdict);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public async Task A_base_that_is_another_branch_fails_and_names_both()
    {
        var result = await Gate("main").Evaluate(Of("""{"base":{"ref":"feat/phase-1"}}"""), Token);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        var reason = Assert.Single(result.Reasons);
        Assert.Contains("feat/phase-1", reason, StringComparison.Ordinal);
        Assert.Contains("main", reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_branch_that_only_starts_like_the_default_is_not_the_default()
    {
        var result = await Gate("main").Evaluate(Of("""{"base":{"ref":"main-2"}}"""), Token);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
    }

    [Fact]
    public async Task A_pr_fact_given_as_stored_json_text_is_read()
    {
        var result = await Gate("main").Evaluate(Of("\"{\\\"base\\\":{\\\"ref\\\":\\\"main\\\"}}\""), Token);

        Assert.Equal(GateVerdict.Pass, result.Verdict);
    }

    [Fact]
    public async Task A_pr_without_a_base_ref_falls_back_to_the_snapshots_base()
    {
        var result = await Gate("main").Evaluate(Of("""{"url":"https://example.invalid/pr/1"}""", prState: """{"base":{"ref":"main"}}"""), Token);

        Assert.Equal(GateVerdict.Pass, result.Verdict);
    }

    [Fact]
    public async Task No_base_ref_anywhere_fails_closed()
    {
        var result = await Gate("main").Evaluate(Of("""{"url":"https://example.invalid/pr/1"}"""), Token);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
    }

    [Fact]
    public async Task No_workspace_fails_closed_without_asking_git()
    {
        var branches = new FakeDefaultBranch();

        var result = await new PrBaseIsDefaultGate(branches).Evaluate(Of("""{"base":{"ref":"main"}}""", workspace: null), Token);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Empty(branches.Calls);
    }

    [Fact]
    public async Task A_default_branch_that_cannot_be_read_fails_closed()
    {
        var result = await Gate(defaultBranch: null).Evaluate(Of("""{"base":{"ref":"main"}}"""), Token);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Contains(Workspace, Assert.Single(result.Reasons), StringComparison.Ordinal);
    }
}
