using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.StartInstance;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Workflow.Registry;

namespace Aw.Sindri.Tests.StartInstance;

/// <summary>
/// 'StartInstance' reads the item's launches (UI spec E-19) before it writes anything (review fix C4), and its catalog row
/// reads the views (E-19: it waits for the launches view head, C1).
/// </summary>
public sealed class StartInstanceLaunchReadTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_launches_read_that_fails_leaves_no_facts_behind()
    {
        var facts = new InMemoryFactStore();
        var service = new StartInstanceService(new OneItem(), new OneRecipe(), new NoInstances(), facts, new NoInstances(), new FailingLaunches(), new ClaimHolders(new InMemoryWorkflowClaims(), new FakeInstanceLiveness()));
        var request = new StartInstanceRequest("workflow.step", "T-1", NextAttempt: false,
            new Dictionary<FactKey, StartFact> { [FactKey.From("fact:spec")] = new("spec.md", Encoding.UTF8.GetBytes("the spec")) });

        var started = await service.Handle(request, Token);

        Assert.Equal(SindriErrors.ViewsBehind.Code, started.Error.Code);
        Assert.Empty(facts.Stored);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_start_row_reads_the_views_and_stays_a_desk_command()
    {
        var row = Assert.Single(new CommandCatalog().Rows, static r => r.Action.Value == "StartInstance");

        Assert.Equal((ActionClass.Desk, true, "ready"), (row.Class, row.ReadsViews, row.Ui));
    }

    private sealed class OneItem : IItems
    {
        public ValueTask<Result<ResolvedItem>> Resolve(string reference, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<ResolvedItem>>(new ResolvedItem(ItemKey.From("T-1"), []));
    }

    private sealed class OneRecipe : IApprovedRecipes
    {
        private static readonly RecipeId _id = RecipeId.From("workflow.step");

        private static ApprovedRecipe Recipe => new(new RecipeVersion(_id, 1, RegistryData.Hash("step")), ARecipe.Minimal(_id));

        public ValueTask<Result<ApprovedRecipe>> Latest(RecipeId id, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovedRecipe>>(Recipe);

        public ValueTask<Result<ApprovedRecipe>> Find(RecipeId id, RecipeHash hash, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovedRecipe>>(Recipe);

        public ValueTask<Result<ApprovalCheck>> Check(RecipeVersion recipe, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovalCheck>>(ApprovalCheck.Holds);
    }

    private sealed class FailingLaunches : ILaunchRequests
    {
        public ValueTask<Result<LaunchId?>> Requested(ItemKey item, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<LaunchId?>>(SindriErrors.ViewsBehind.With("the launches view didn't answer"));
    }
}
