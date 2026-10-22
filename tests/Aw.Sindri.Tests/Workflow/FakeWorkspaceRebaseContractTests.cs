using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

[Lane(TestLane.Unit)]
public sealed class FakeWorkspaceRebaseContractTests : WorkspaceRebaseContract
{
    private const string Workspace = "/work/tree";

    protected override ValueTask<(IWorkspaceRebase Rebase, string Workspace, string Onto)> Arrange(RebaseScenario scenario)
    {
        var fake = new FakeWorkspaceRebase();
        fake.Script(Workspace, scenario switch
        {
            RebaseScenario.Moves => new RebaseOutcome(RebaseStatus.Rebased, "c2", []),
            RebaseScenario.Conflicts => new RebaseOutcome(RebaseStatus.Conflict, NewHead: null, ["a.txt"]),
            RebaseScenario.UpToDate => new RebaseOutcome(RebaseStatus.Nothing, NewHead: null, []),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        });
        return ValueTask.FromResult<(IWorkspaceRebase, string, string)>((fake, Workspace, "main"));
    }
}
