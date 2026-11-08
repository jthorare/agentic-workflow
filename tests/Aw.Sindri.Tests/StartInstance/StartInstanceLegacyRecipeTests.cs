using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Features.StartInstance;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Workflow.Registry;

namespace Aw.Sindri.Tests.StartInstance;

/// <summary>
/// An approved recipe registered before input kinds (RCP-INPUT-001/002) is refused at the start with the fix, never started:
/// the workflow would throw naming its subject and retry forever.
/// </summary>
public sealed class StartInstanceLegacyRecipeTests
{
    private static readonly FactKey _item = FactKey.From("fact:item");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_recipe_whose_input_has_no_kind_is_refused_naming_RCP_INPUT_001_and_the_fix()
    {
        var (service, runtime) = Service([new RecipeInput(_item, Label: null, Type: null, Kind: null, Generate: null)]);

        var started = await service.Handle(Request(), Token);

        Assert.Equal(WorkflowErrors.RecipeInvalid.Code, started.Error.Code);
        Assert.Contains("RCP-INPUT-001", started.Error.Message, StringComparison.Ordinal);
        Assert.Contains("fix: register the current file at a new version and approve it", started.Error.Message, StringComparison.Ordinal);
        Assert.Null(runtime.Started);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_recipe_with_no_inputs_is_refused_naming_RCP_INPUT_002_and_the_fix()
    {
        var (service, runtime) = Service([]);

        var started = await service.Handle(Request(), Token);

        Assert.Equal(WorkflowErrors.RecipeInvalid.Code, started.Error.Code);
        Assert.Contains("RCP-INPUT-002", started.Error.Message, StringComparison.Ordinal);
        Assert.Contains("fix: register the current file at a new version and approve it", started.Error.Message, StringComparison.Ordinal);
        Assert.Null(runtime.Started);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_legacy_recipe_whose_pins_drifted_is_refused_as_legacy_not_as_drift()
    {
        var (service, runtime) = Service([], ApprovalCheck.Drifted);

        var started = await service.Handle(Request(), Token);

        Assert.Equal(WorkflowErrors.RecipeInvalid.Code, started.Error.Code);
        Assert.Contains(SubjectKinds.LegacyFix, started.Error.Message, StringComparison.Ordinal);
        Assert.Null(runtime.Started);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_revoked_legacy_recipe_is_refused_as_revoked_before_legacy()
    {
        var (service, runtime) = Service([], ApprovalCheck.Revoked);

        var started = await service.Handle(Request(), Token);

        Assert.Equal(WorkflowErrors.RecipeRevoked.Code, started.Error.Code);
        Assert.Null(runtime.Started);
    }

    private static StartInstanceRequest Request() => new("workflow.old", "T-1", NextAttempt: false, new Dictionary<FactKey, StartFact> { [_item] = new("item.json", Encoding.UTF8.GetBytes("{\"id\":\"T-1\"}")) });

    private static (StartInstanceService Service, Capturing Runtime) Service(IReadOnlyList<RecipeInput> inputs, ApprovalCheck check = ApprovalCheck.Holds)
    {
        var runtime = new Capturing();
        return (new StartInstanceService(new OneItem(), new OneRecipe(inputs, check), new NoInstances(), new StartFacts(new InMemoryFactStore(), TimeProvider.System, new StepInputRuntimes([])), runtime, new NoLaunches(), new ClaimHolders(new InMemoryWorkflowClaims(), new FakeInstanceLiveness())), runtime);
    }

    private sealed class Capturing : IInstanceRuntime
    {
        public InstanceStart? Started { get; private set; }

        public ValueTask<Result<InstanceView>> Start(InstanceStart start, CancellationToken cancellationToken)
        {
            Started = start;
            return ValueTask.FromResult<Result<InstanceView>>(SindriErrors.ViewsBehind.With("the capture stops here"));
        }

        public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<CommandResult>> Send(InstanceAddress address, CommandNonce nonce, Trigger command, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<CommandResult>> OpenRequest(InstanceAddress address, LaunchId launch, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<Unit>> Signal(InstanceAddress address, WaitSatisfied delivered, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class OneItem : IItems
    {
        public ValueTask<Result<ResolvedItem>> Resolve(string reference, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<ResolvedItem>>(new ResolvedItem(ItemKey.From("T-1"), []));
    }

    private sealed class NoLaunches : ILaunchRequests
    {
        public ValueTask<Result<LaunchId?>> Requested(ItemKey item, CancellationToken cancellationToken) => ValueTask.FromResult<Result<LaunchId?>>((LaunchId?)null);
    }

    private sealed class OneRecipe(IReadOnlyList<RecipeInput> inputs, ApprovalCheck check) : IApprovedRecipes
    {
        private static readonly RecipeId _id = RecipeId.From("workflow.old");

        private readonly ApprovedRecipe _recipe = new(new RecipeVersion(_id, 1, RegistryData.Hash("old")), ARecipe.Minimal(_id) with { Inputs = inputs });

        public ValueTask<Result<ApprovedRecipe>> Latest(RecipeId id, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovedRecipe>>(_recipe);

        public ValueTask<Result<ApprovedRecipe>> Find(RecipeId id, RecipeHash hash, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovedRecipe>>(_recipe);

        public ValueTask<Result<ApprovalCheck>> Check(RecipeVersion recipe, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovalCheck>>(check);
    }
}
