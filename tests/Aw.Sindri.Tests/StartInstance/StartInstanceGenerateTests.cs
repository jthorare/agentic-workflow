using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Features.StartInstance;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Workflow.Registry;
using Aw.Sindri.Tests.ListInstances;

namespace Aw.Sindri.Tests.StartInstance;

/// <summary>An input's generate template (spec section 3.11): a start that supplies no value gets the rendered text, stored like a supplied fact and marked generated; a supplied value always wins.</summary>
public sealed class StartInstanceGenerateTests
{
    private static readonly FactKey _item = FactKey.From("fact:item");
    private static readonly FactKey _run = FactKey.From("fact:run");
    private static readonly DateTimeOffset _now = new(2026, 10, 15, 9, 5, 30, TimeSpan.FromHours(2));   // 07:05 in UTC

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Dictionary<FactKey, StartFact> Item() => new() { [_item] = new("item.json", Encoding.UTF8.GetBytes("{\"id\":\"T-1\"}")) };

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_generated_input_is_rendered_from_the_start_time_stored_and_marked_generated()
    {
        var (service, facts, runtime) = Service();

        _ = await service.Handle(new StartInstanceRequest("workflow.gen", "T-1", NextAttempt: false, Item()), Token);

        var start = Assert.IsType<InstanceStart>(runtime.Started);
        var generated = start.Inputs[_run];
        Assert.Equal(("generated", true), (generated.Name, generated.Generated));
        Assert.Equal("run 2026-10-15 07:05", Encoding.UTF8.GetString(facts.Stored[generated.Value.Sha256]));
        Assert.False(start.Inputs[_item].Generated);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_start_stamps_one_instant_and_the_generated_input_reads_it_across_a_date_boundary()
    {
        var clock = new SteppingClock(new DateTimeOffset(2026, 10, 15, 23, 59, 59, TimeSpan.Zero));   // every read is a minute later: a second read crosses midnight
        var facts = new InMemoryFactStore();
        var runtime = new Capturing();
        var service = new StartInstanceService(new OneItem(), new OneRecipe(), new NoInstances(), new StartFacts(facts, clock, new StepInputRuntimes([])), runtime, new NoLaunches(), new ClaimHolders(new InMemoryWorkflowClaims(), new FakeInstanceLiveness()));

        _ = await service.Handle(new StartInstanceRequest("workflow.gen", "T-1", NextAttempt: false, Item()), Token);

        var start = Assert.IsType<InstanceStart>(runtime.Started);
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 23, 59, 59, TimeSpan.Zero), start.StartedAt);
        Assert.Equal("run 2026-10-15 23:59", Encoding.UTF8.GetString(facts.Stored[start.Inputs[_run].Value.Sha256]));
        Assert.Equal(1, clock.Reads);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_start_records_the_calling_actor_as_who_started_it()
    {
        var runtime = new Capturing();
        var caller = new FixedCaller(new CallerContextBuilder().Human().Build());
        var service = new StartInstanceService(new OneItem(), new OneRecipe(), new NoInstances(), new StartFacts(new InMemoryFactStore(), new FixedClock(), new StepInputRuntimes([]), caller), runtime, new NoLaunches(), new ClaimHolders(new InMemoryWorkflowClaims(), new FakeInstanceLiveness()));

        _ = await service.Handle(new StartInstanceRequest("workflow.gen", "T-1", NextAttempt: false, Item()), Token);

        Assert.Equal(caller.Current!.Actor, Assert.IsType<InstanceStart>(runtime.Started).StartedBy);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_start_outside_any_call_records_nobody()
    {
        var (service, _, runtime) = Service();

        _ = await service.Handle(new StartInstanceRequest("workflow.gen", "T-1", NextAttempt: false, Item()), Token);

        Assert.Null(Assert.IsType<InstanceStart>(runtime.Started).StartedBy);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_supplied_value_wins_over_the_template()
    {
        var (service, facts, runtime) = Service();
        var request = Item();
        request[_run] = new("run.txt", Encoding.UTF8.GetBytes("my own run"));

        _ = await service.Handle(new StartInstanceRequest("workflow.gen", "T-1", NextAttempt: false, request), Token);

        var supplied = Assert.IsType<InstanceStart>(runtime.Started).Inputs[_run];
        Assert.Equal(("run.txt", false), (supplied.Name, supplied.Generated));
        Assert.Equal("my own run", Encoding.UTF8.GetString(facts.Stored[supplied.Value.Sha256]));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_input_without_a_template_is_still_required()
    {
        var (service, _, runtime) = Service();

        var started = await service.Handle(new StartInstanceRequest("workflow.gen", "T-1", NextAttempt: false, new Dictionary<FactKey, StartFact>()), Token);

        Assert.Equal(SindriErrors.InvalidInput.Code, started.Error.Code);
        Assert.Contains("fact:item", started.Error.Message, StringComparison.Ordinal);
        Assert.Null(runtime.Started);
    }

    private static (StartInstanceService Service, InMemoryFactStore Facts, Capturing Runtime) Service()
    {
        var facts = new InMemoryFactStore();
        var runtime = new Capturing();
        return (new StartInstanceService(new OneItem(), new OneRecipe(), new NoInstances(), new StartFacts(facts, new FixedClock(), new StepInputRuntimes([])), runtime, new NoLaunches(), new ClaimHolders(new InMemoryWorkflowClaims(), new FakeInstanceLiveness())), facts, runtime);
    }

    private sealed class SteppingClock(DateTimeOffset first) : TimeProvider
    {
        public int Reads { get; private set; }

        public override DateTimeOffset GetUtcNow() => first.AddMinutes(Reads++);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => _now;
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
        private static readonly RecipeId _id = RecipeId.From("workflow.gen");

        private static ApprovedRecipe Recipe => new(
            new RecipeVersion(_id, 1, RegistryData.Hash("gen")),
            ARecipe.Minimal(_id) with
            {
                Inputs =
                [
                    new RecipeInput(_item, Label: null, Type: "item", Kind: "item", Generate: null),
                    new RecipeInput(_run, Label: null, Type: null, Kind: "text", Generate: "run {start:date} {start:time}"),
                ],
            });

        public ValueTask<Result<ApprovedRecipe>> Latest(RecipeId id, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovedRecipe>>(Recipe);

        public ValueTask<Result<ApprovedRecipe>> Find(RecipeId id, RecipeHash hash, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovedRecipe>>(Recipe);

        public ValueTask<Result<ApprovalCheck>> Check(RecipeVersion recipe, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovalCheck>>(ApprovalCheck.Holds);
    }
}
