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
using Aw.Sindri.Features.StartInstance;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Workflow.Registry;

namespace Aw.Sindri.Tests.StartInstance;

/// <summary>A start of an approved recipe that names a Step input this build has no runtime for is refused with SND-WF-016, never started (engine spec §5.1, E1).</summary>
public sealed class StartInstanceStepInputTests
{
    private static readonly FactKey _item = FactKey.From("fact:item");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task Start_refuses_a_recipe_whose_input_is_descriptor_only_with_SND_WF_016()
    {
        var (service, runtime) = Service(runtimes: []);

        var started = await service.Handle(Request(), Token);

        Assert.Equal(WorkflowErrors.StepInputNoRuntime.Code, started.Error.Code);
        Assert.Contains("states.produce.step.inputs[0]", started.Error.Message, StringComparison.Ordinal);
        Assert.Contains(FakeStepInput.Id, started.Error.Message, StringComparison.Ordinal);
        Assert.Null(runtime.Started);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_goes_on_when_the_input_has_a_runtime()
    {
        var (service, runtime) = Service(runtimes: [new StepInputRegistration(BlockId.From(FakeStepInput.Id))]);

        _ = await service.Handle(Request(), Token);

        Assert.NotNull(runtime.Started);
    }

    private static StartInstanceRequest Request() =>
        new("workflow.inputs", "T-1", NextAttempt: false, new Dictionary<FactKey, StartFact> { [_item] = new("item.json", Encoding.UTF8.GetBytes("{\"id\":\"T-1\"}")) });

    private static (StartInstanceService Service, Capturing Runtime) Service(StepInputRegistration[] runtimes)
    {
        var runtime = new Capturing();
        return (new StartInstanceService(new OneItem(), new OneRecipe(), new NoInstances(), new StartFacts(new InMemoryFactStore(), TimeProvider.System, new StepInputRuntimes(runtimes)), runtime, new NoLaunches(),
            new ClaimHolders(new InMemoryWorkflowClaims(), new FakeInstanceLiveness())), runtime);
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

    private sealed class OneRecipe : IApprovedRecipes
    {
        private static readonly RecipeId _id = RecipeId.From("workflow.inputs");

        private readonly ApprovedRecipe _recipe = new(new RecipeVersion(_id, 1, RegistryData.Hash("inputs")), ARecipe.WithStepInputs(ARecipe.Minimal(_id), FakeStepInput.Id));

        public ValueTask<Result<ApprovedRecipe>> Latest(RecipeId id, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovedRecipe>>(_recipe);

        public ValueTask<Result<ApprovedRecipe>> Find(RecipeId id, RecipeHash hash, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovedRecipe>>(_recipe);

        public ValueTask<Result<ApprovalCheck>> Check(RecipeVersion recipe, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovalCheck>>(ApprovalCheck.Holds);
    }
}
