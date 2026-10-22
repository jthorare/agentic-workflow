using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Worktrees;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.Workflow.Phase;

/// <summary>The phase worktree's end (line spec LN-WF2): a phase instance removes it when it ends, but keeps it while its pull request may still be open.</summary>
[Lane(TestLane.Unit)]
public sealed class PhaseWorkspaceTests
{
    private static readonly FactKey _workspace = FactKey.From("fact:workspace");
    private static readonly FactKey _pr = FactKey.From("fact:pr");
    private static readonly RecipeId _phase = RecipeId.From("workflow.phase");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static FactValue Stored(string content)
    {
        var sha = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
        return new FactValue("fact:sha256:" + sha, sha, HeadSha: null);
    }

    private static Dictionary<FactKey, FactValue> Facts(bool pr)
    {
        var facts = new Dictionary<FactKey, FactValue> { [_workspace] = Stored("/work/app/.claude/worktrees/sindri-l-p1") };
        if (pr)
        {
            facts[_pr] = Stored("https://example.test/pr/1");
        }

        return facts;
    }

    [Fact]
    public void A_merged_phase_gives_its_worktree_back_even_though_it_has_a_pull_request() =>
        Assert.Equal(Facts(pr: true)[_workspace], PhaseWorkspace.ToRemove(_phase, Facts(pr: true), OutcomeName.From("resolved")));

    [Fact]
    public void A_dropped_phase_whose_pull_request_may_be_open_keeps_its_worktree() =>
        Assert.Null(PhaseWorkspace.ToRemove(_phase, Facts(pr: true), OutcomeName.From("unresolved")));

    [Fact]
    public void A_cancelled_phase_whose_pull_request_may_be_open_keeps_its_worktree() =>
        Assert.Null(PhaseWorkspace.ToRemove(_phase, Facts(pr: true), outcome: null));

    [Theory]
    [InlineData("unresolved")]
    [InlineData("cancelled")]
    public void A_phase_that_never_opened_a_pull_request_gives_its_worktree_back_when_it_ends(string outcome) =>
        Assert.Equal(Facts(pr: false)[_workspace], PhaseWorkspace.ToRemove(_phase, Facts(pr: false), OutcomeName.From(outcome)));

    [Fact]
    public void A_phase_that_never_seeded_has_nothing_to_remove() =>
        Assert.Null(PhaseWorkspace.ToRemove(_phase, new Dictionary<FactKey, FactValue>(), OutcomeName.From("resolved")));

    [Theory]
    [InlineData("workflow.pr-babysitter")]
    [InlineData("workflow.docs-sync")]
    [InlineData("workflow.build")]
    public void Only_the_phase_owns_its_worktree_a_child_or_another_recipe_never_removes_it(string recipe) =>
        Assert.Null(PhaseWorkspace.ToRemove(RecipeId.From(recipe), Facts(pr: false), OutcomeName.From("resolved")));

    [Theory]
    [InlineData("/work/phase")]
    [InlineData("\"/work/phase\"")]
    [InlineData("/work/phase\n")]
    public async Task The_path_of_a_stored_workspace_is_its_text_or_its_JSON_string(string content)
    {
        var path = await PhaseWorkspace.PathOf(new Facts1(content), Stored(content), Token);

        Assert.Equal("/work/phase", path.Value);
    }

    [Fact]
    public async Task A_workspace_that_cannot_be_read_is_an_error_not_a_path()
    {
        var path = await PhaseWorkspace.PathOf(new Facts1(content: null), Stored("/x"), Token);

        Assert.True(path.IsError);
    }

    [Fact]
    public async Task Removal_asks_the_port_to_remove_the_path_the_fact_names()
    {
        var worktrees = new Recording();
        var removal = new PhaseWorkspaceRemoval(new Facts1("/work/phase"), worktrees);

        var gone = await removal.Remove(Stored("/work/phase"), Token);

        Assert.True(gone);
        Assert.Equal(["/work/phase"], worktrees.Removed);
    }

    [Fact]
    public async Task A_tree_with_uncommitted_files_is_left_as_it_is_and_the_ending_is_not_failed()
    {
        var worktrees = new Recording { Answer = Core.SindriErrors.WorktreeDirty.With("dirty") };
        var removal = new PhaseWorkspaceRemoval(new Facts1("/work/phase"), worktrees);

        Assert.False(await removal.Remove(Stored("/work/phase"), Token));
    }

    [Fact]
    public async Task A_workspace_that_cannot_be_read_removes_nothing()
    {
        var worktrees = new Recording();
        var removal = new PhaseWorkspaceRemoval(new Facts1(content: null), worktrees);

        Assert.False(await removal.Remove(Stored("/work/phase"), Token));
        Assert.Empty(worktrees.Removed);
    }

    private sealed class Facts1(string? content) : IFactContents
    {
        public ValueTask<Result<ReadOnlyMemory<byte>>> Read(FactValue fact, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<ReadOnlyMemory<byte>>>(content is null
                ? Sindri.Core.SindriErrors.WorktreeInvalid.With("not stored")
                : (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(content));
    }

    private sealed class Recording : IWorktrees
    {
        public List<string> Removed { get; } = [];

        public Result<Unit> Answer { get; set; } = Results.Ok();

        public ValueTask<Result<WorktreeCheckout>> Create(WorktreeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<Unit>> Remove(string path, CancellationToken cancellationToken)
        {
            Removed.Add(path);
            return ValueTask.FromResult(Answer);
        }
    }
}
