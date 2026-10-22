using Aw.Kernel.Testing;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Testing.Checks;

namespace Aw.Sindri.Tests.Checks;

[Lane(TestLane.Unit)]
public sealed class InMemoryBranchChangesContractTests : BranchChangesContract
{
    private const string Tree = "/work";

    protected override ValueTask<(IBranchChanges Port, string Worktree, string Base, string Head)> Linear() => ValueTask.FromResult<(IBranchChanges, string, string, string)>((
        new InMemoryBranchChanges().Add(Tree, "b0", "c2", new BranchChanges(
            [new ChangedFile("src/a.cs", ChangeKind.Modified), new ChangedFile("src/b.cs", ChangeKind.Added), new ChangedFile("tests/t.test.ts", ChangeKind.Deleted)],
            [new AddedLine("src/a.cs", 2, "two"), new AddedLine("src/b.cs", 1, "x1"), new AddedLine("src/b.cs", 2, "x2")],
            [new ChangedCommit("c1", "feat: add b", IsMerge: false), new ChangedCommit("c2", "fix: tweak a", IsMerge: false)])),
        Tree, "b0", "c2"));

    protected override ValueTask<(IBranchChanges Port, string Worktree, string Base, string Head)> WithMerge() => ValueTask.FromResult<(IBranchChanges, string, string, string)>((
        new InMemoryBranchChanges().Add(Tree, "b0", "m1", new BranchChanges([], [], [new ChangedCommit("s1", "feat: side", IsMerge: false), new ChangedCommit("m1", "Merge side", IsMerge: true)])),
        Tree, "b0", "m1"));

    protected override ValueTask<(IBranchChanges Port, string Worktree, string Base, string Head)> Unknown() => ValueTask.FromResult<(IBranchChanges, string, string, string)>((
        new InMemoryBranchChanges(), Tree, "nope", "nada"));
}
