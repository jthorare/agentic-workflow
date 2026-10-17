using System.Text;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.StartInstance;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Workflow.Registry;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.Launching;

/// <summary>The engine's view of the item's launches (UI spec E-19): a launch still requested and inside its deadline starts the instance taken over.</summary>
[Lane(TestLane.Unit)]
public sealed class LaunchRequestsViewTests
{
    private static readonly LaunchId _launch = LaunchId.From("LCH-" + new string('0', 31) + "1");
    private static readonly FactKey _fact = FactKey.From("fact:item");
    private readonly ViewsHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private LaunchRequestsView Adapter => new(new LaunchViews(_h.Views), _h.Slice.Clock);

    private async Task<InstanceStart?> Start()
    {
        var runtime = new Capturing();
        var service = new StartInstanceService(new OneItem(), new OneRecipe(), new NoInstances(), new StartFacts(new InMemoryFactStore(), _h.Slice.Clock), runtime, Adapter,
            new ClaimHolders(new InMemoryWorkflowClaims(), new FakeInstanceLiveness()));
        var handled = await service.Handle(new StartInstanceRequest("workflow.step", A.FirstItemId.Value, NextAttempt: false,
            new Dictionary<FactKey, StartFact> { [_fact] = new("item.json", Encoding.UTF8.GetBytes($"{{\"id\":\"{A.FirstItemId.Value}\"}}")) }), Token);
        Assert.True(runtime.Started is not null, handled.IsError ? handled.Error.ToString() : "no start");
        return runtime.Started;
    }

    [Fact]
    public async Task StartInstance_while_requested_starts_taken_over()
    {
        await _h.Item(repo: null);
        await _h.Request(_launch);

        var start = await Start();

        Expect.Equal(_launch.Value, start!.OpenLaunch);
    }

    [Fact]
    public async Task A_launch_with_an_outcome_or_past_its_deadline_is_not_requested()
    {
        await _h.Item(repo: null);
        await _h.Request(_launch);
        _h.Slice.Clock.Advance(LaunchRules.Deadline + TimeSpan.FromSeconds(1));
        Assert.Null((await Start())!.OpenLaunch);   // the deadline passed: the sweep will fail it

        var other = LaunchId.From("LCH-" + new string('0', 31) + "2");
        await _h.Request(other);
        await _h.Fail(launch: other);
        Assert.Null((await Start())!.OpenLaunch);
    }

    [Fact]
    public async Task A_key_that_is_no_item_id_has_no_launch()
    {
        var read = await Adapter.Requested(ItemKey.From("T-1"), Token);

        Assert.True(read.IsSuccess);
        Assert.Null(read.Value);
    }

    private sealed class OneItem : IItems
    {
        public ValueTask<Result<ResolvedItem>> Resolve(string reference, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<ResolvedItem>>(new ResolvedItem(ItemKey.From(A.FirstItemId.Value), []));
    }

    private sealed class OneRecipe : IApprovedRecipes
    {
        private static readonly RecipeId _id = RecipeId.From("workflow.step");

        private static ApprovedRecipe Recipe => new(new RecipeVersion(_id, 1, RegistryData.Hash("step")), ARecipe.Minimal(_id));

        public ValueTask<Result<ApprovedRecipe>> Latest(RecipeId id, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovedRecipe>>(Recipe);

        public ValueTask<Result<ApprovedRecipe>> Find(RecipeId id, RecipeHash hash, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovedRecipe>>(Recipe);

        public ValueTask<Result<ApprovalCheck>> Check(RecipeVersion recipe, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovalCheck>>(ApprovalCheck.Holds);
    }

    private sealed class Capturing : IInstanceRuntime
    {
        public InstanceStart? Started { get; private set; }

        public ValueTask<Result<InstanceView>> Start(InstanceStart start, CancellationToken cancellationToken)
        {
            Started = start;
            return ValueTask.FromResult<Result<InstanceView>>(Core.SindriErrors.ViewsBehind.With("the capture stops here"));
        }

        public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<CommandResult>> Send(InstanceAddress address, CommandNonce nonce, Trigger command, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<CommandResult>> OpenRequest(InstanceAddress address, LaunchId launch, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<Unit>> Signal(InstanceAddress address, WaitSatisfied delivered, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
