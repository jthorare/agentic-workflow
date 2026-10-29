using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Worktrees;
using Aw.Sindri.Features.Build;
using Aw.Sindri.Testing.Prepare;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Worktrees;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Workflow.BuildShip;

/// <summary>'step.phase.seed' on the block runtime (line spec §7.1): the phase's worktree is made once, reused when the instance holds one, and the facts the later waits read are seeded.</summary>
[Lane(TestLane.Unit)]
public sealed class PhaseSeedStepTests
{
    private const string Checkout = "/repos/dev";
    private const string Base = "1111111111111111111111111111111111111111";
    private const string ParentHead = "3333333333333333333333333333333333333333";
    private const string Path = "/repos/dev/.claude/worktrees/sindri-L-1-p2";

    private static readonly string[] _empty = ["buildNotes", "built", "change", "redTests"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static FakeWorktrees Repo() => new FakeWorktrees().WithRepo(Checkout, Base);

    private static PhaseSeedStep Step(FakeWorktrees worktrees) => new(worktrees, new InMemoryRepoCheckouts().With("name", Checkout));

    private static StepContext Context(Action<Dictionary<string, string>>? change = null)
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["phasePacket"] = """{"hash":"sha256:aa","dir":"/packets/p2"}""",
            ["lineContext"] = LineContexts.Of(Base),
        };
        change?.Invoke(facts);
        return BuildShipContexts.Of("step.phase.seed", facts) with { Repo = RepoName.From("name") };
    }

    private static JsonNode Out(BlockStepResult result, string key) => JsonNode.Parse(result.Result!.Outputs[Core.Workflow.Ids.FactKey.From("fact:" + key)].GetRawText())!;

    [Fact]
    public async Task Creates_the_phase_worktree_on_its_branch_at_the_line_base_when_there_is_no_workspace()
    {
        var worktrees = Repo();

        var result = await Step(worktrees).Run(Context(), Token);

        Assert.Equal("seeded", result.Result!.Outcome.Value);
        Assert.Equal(Path, (string?)Out(result, "workspace"));
        var again = await worktrees.Create(new WorktreeRequest(Checkout, Base, "sindri/L-1/p2", Path), Token);   // the same request is idempotent only if the Step made exactly this checkout
        Assert.Equal(new WorktreeCheckout(Path, Base, "sindri/L-1/p2"), again.Value);
    }

    private const string StackedPacket = """{"hash":"sha256:aa","dir":"/packets/p2","base":{"rule":"dependency-head","ref":"sindri/L-1/p1"},"stackParent":"p1"}""";

    [Fact]
    public async Task A_stacked_phase_starts_at_its_parents_head_not_the_line_base()
    {
        var worktrees = Repo().WithRepo(Checkout, Base, ParentHead).WithBranch(Checkout, "sindri/L-1/p1", ParentHead);

        var result = await Step(worktrees).Run(Context(f => f["phasePacket"] = StackedPacket), Token);

        Assert.Equal("seeded", result.Result!.Outcome.Value);
        var again = await worktrees.Create(new WorktreeRequest(Checkout, ParentHead, "sindri/L-1/p2", Path), Token);   // idempotent only if the Step made exactly this checkout
        Assert.Equal(new WorktreeCheckout(Path, ParentHead, "sindri/L-1/p2"), again.Value);
    }

    [Fact]
    public async Task A_phase_on_the_default_branch_still_starts_at_the_line_base()
    {
        var worktrees = Repo().WithRepo(Checkout, Base, ParentHead).WithBranch(Checkout, "sindri/L-1/p1", ParentHead);

        var result = await Step(worktrees).Run(Context(f => f["phasePacket"] = """{"hash":"sha256:aa","dir":"/packets/p2","base":{"rule":"default-branch","ref":""},"stackParent":null}"""), Token);

        Assert.Equal("seeded", result.Result!.Outcome.Value);
        var again = await worktrees.Create(new WorktreeRequest(Checkout, Base, "sindri/L-1/p2", Path), Token);
        Assert.Equal(new WorktreeCheckout(Path, Base, "sindri/L-1/p2"), again.Value);
    }

    [Fact]
    public async Task A_dependency_head_that_does_not_resolve_fails_the_Step_and_never_falls_back_to_the_line_base()
    {
        var worktrees = Repo();

        var result = await Step(worktrees).Run(Context(f => f["phasePacket"] = StackedPacket), Token);

        Assert.Null(result.Result);
        Assert.Contains("sindri/L-1/p1", result.Failure, StringComparison.Ordinal);
        var made = await worktrees.Create(new WorktreeRequest(Checkout, Base, "sindri/L-1/p2", Path), Token);   // nothing was made at any base, so this is a fresh create
        Assert.True(made.IsSuccess);
    }

    [Theory]
    [InlineData("""{"hash":"sha256:aa","base":{"rule":"dependency-head","ref":""}}""")]
    [InlineData("""{"hash":"sha256:aa","base":{"rule":"dependency-head"}}""")]
    public async Task A_dependency_head_without_a_ref_fails_the_Step(string packet)
    {
        var result = await Step(Repo()).Run(Context(f => f["phasePacket"] = packet), Token);

        Assert.Null(result.Result);
        Assert.Contains("dependency-head", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Two_phases_of_one_line_get_two_worktrees_and_two_branches()
    {
        var worktrees = Repo();

        var first = await Step(worktrees).Run(Context(f => f["lineContext"] = f["lineContext"].Replace("\"p2\"", "\"p1\"", StringComparison.Ordinal)), Token);
        var second = await Step(worktrees).Run(Context(), Token);

        Assert.Equal(["/repos/dev/.claude/worktrees/sindri-L-1-p1", Path], [(string?)Out(first, "workspace"), (string?)Out(second, "workspace")]);
    }

    [Fact]
    public async Task Seeds_the_facts_the_later_waits_read()
    {
        var result = await Step(Repo()).Run(Context(), Token);

        Assert.Equal(["buildNotes", "built", "change", "redTests", "workspace"], result.Result!.Outputs.Keys.Select(static k => k.Value[5..]).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.All(_empty, key => Assert.Empty(Out(result, key).AsObject()));
    }

    [Fact]
    public async Task A_retried_seed_returns_the_same_workspace_and_makes_no_second_worktree()
    {
        var worktrees = Repo();

        var first = await Step(worktrees).Run(Context(), Token);
        var retry = await Step(worktrees).Run(Context(), Token);   // a worker crash or a Temporal retry runs the Step again for the same instance

        Assert.Equal("seeded", retry.Result!.Outcome.Value);
        Assert.Equal(Out(first, "workspace").ToJsonString(), Out(retry, "workspace").ToJsonString());
        Assert.Equal(Path, (string?)Out(retry, "workspace"));
    }

    [Fact]
    public async Task A_path_in_use_at_another_base_stops_the_Step_with_SND_WT_002_and_resets_nothing()
    {
        var worktrees = Repo();
        _ = await worktrees.Create(new WorktreeRequest(Checkout, Base, "sindri/L-1/p2", Path), Token);
        worktrees.AdvanceHead(Path);

        var result = await Step(worktrees).Run(Context(), Token);

        Assert.Null(result.Result);
        Assert.Contains("SND-WT-002", result.Failure, StringComparison.Ordinal);
        Assert.Contains("parked", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_base_git_does_not_know_fails_the_Step_with_SND_WT_003()
    {
        var result = await Step(new FakeWorktrees().WithRepo(Checkout, "2222222222222222222222222222222222222222")).Run(Context(), Token);

        Assert.Null(result.Result);
        Assert.Contains("SND-WT-003", result.Failure, StringComparison.Ordinal);
        Assert.DoesNotContain("parked", result.Failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("lineContext")]
    [InlineData("phasePacket")]
    public async Task Without_a_needed_fact_the_Step_fails_and_names_it(string missing)
    {
        var result = await Step(Repo()).Run(Context(f => f.Remove(missing)), Token);

        Assert.Null(result.Result);
        Assert.Contains(missing, result.Failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"phase":"p2","base":"1111111111111111111111111111111111111111"}""", "line")]
    [InlineData("""{"line":"L-1","base":"1111111111111111111111111111111111111111"}""", "phase")]
    [InlineData("""{"line":"L-1","phase":"p2","base":"main"}""", "base")]
    [InlineData("{}", "line")]
    public async Task A_line_context_without_line_phase_or_a_full_sha_base_fails_the_Step_and_names_the_field(string lineContext, string field)
    {
        var result = await Step(Repo()).Run(Context(f => f["lineContext"] = lineContext), Token);

        Assert.Null(result.Result);
        Assert.Contains(field, result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_instance_with_no_repo_checkout_fails_the_Step()
    {
        var result = await new PhaseSeedStep(Repo(), new InMemoryRepoCheckouts()).Run(Context(), Token);

        Assert.Null(result.Result);
        Assert.Contains("checkout", result.Failure, StringComparison.Ordinal);
    }
}
