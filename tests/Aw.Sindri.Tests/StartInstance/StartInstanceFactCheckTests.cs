using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Features.StartInstance;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Workflow.Registry;
using Aw.Sindri.Tests.GetInstance;

namespace Aw.Sindri.Tests.StartInstance;

/// <summary>
/// A start checks each seeded input fact before it writes anything (the real e2e started 'workflow.scoping' with a 'fact:scopeInput' that lacked its repo and nothing said so): a fact a registered
/// check finds wrong refuses the start with SND-CLI-003, naming the fact and each problem; a fact no check is about starts as before.
/// </summary>
public sealed class StartInstanceFactCheckTests
{
    private const string Brief = """{"kind":"brief","repo":"acme/web","path":"briefs/expiry.md"}""";
    private const string NoRepo = """{"kind":"brief","path":"briefs/expiry.md"}""";
    private const string ItemSha = "0000000000000000000000000000000000000000000000000000000000000001";
    private const string NoRepoSha = "0000000000000000000000000000000000000000000000000000000000000002";
    private static readonly FactKey _item = FactKey.From("fact:item");
    private static readonly FactKey _scopeInput = FactKey.From("fact:scopeInput");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_scope_input_without_its_repo_is_refused_and_nothing_is_started()
    {
        var (service, runtime) = Service();

        var started = await service.Handle(Request(NoRepo), Token);

        Assert.Equal(SindriErrors.InvalidInput.Code, started.Error.Code);
        Assert.Contains("fact:scopeInput (scope.json)", started.Error.Message, StringComparison.Ordinal);
        Assert.Contains("repo", started.Error.Message, StringComparison.Ordinal);
        Assert.Contains("fix:", started.Error.Message, StringComparison.Ordinal);
        Assert.Null(runtime.Started);
        Assert.False(runtime.Asked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_scope_input_that_is_not_json_is_refused_naming_the_fact()
    {
        var (service, runtime) = Service();

        var started = await service.Handle(Request("not json"), Token);

        Assert.Equal(SindriErrors.InvalidInput.Code, started.Error.Code);
        Assert.Contains("fact:scopeInput", started.Error.Message, StringComparison.Ordinal);
        Assert.Contains("isn't JSON", started.Error.Message, StringComparison.Ordinal);
        Assert.Null(runtime.Started);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_valid_scope_input_still_starts()
    {
        var (service, runtime) = Service();

        _ = await service.Handle(Request(Brief), Token);

        Assert.NotNull(runtime.Started);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_start_with_no_check_for_its_facts_is_unaffected()
    {
        var (service, runtime) = Service(withCheck: false);

        _ = await service.Handle(Request(NoRepo), Token);

        Assert.NotNull(runtime.Started);   // nothing is registered for the fact, so the bad one starts as it always did
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rerun_whose_stored_scope_input_fails_its_check_is_refused_the_same_way_and_nothing_is_started()
    {
        var (start, runtime) = Service();
        var ended = StatusCases.View("cancelled") with
        {
            ItemId = ItemKey.From("T-1"),
            Inputs =
            [
                new InputPresence("fact:item", "Item", "item", Required: true, Present: true),
                new InputPresence("fact:scopeInput", "Scope input", "payload", Required: true, Present: true),
            ],
            FactShas = new Dictionary<string, string>(StringComparer.Ordinal) { ["fact:item"] = ItemSha, ["fact:scopeInput"] = NoRepoSha },
        };
        var instances = new SomeInstances(ended);
        var rerun = new RerunInstanceService(instances, instances, new Stored(), start);

        var started = await rerun.Handle(new RerunInstanceRequest(ended.Id.Value), Token);

        Assert.Equal(SindriErrors.InvalidInput.Code, started.Error.Code);
        Assert.Contains("fact:scopeInput", started.Error.Message, StringComparison.Ordinal);
        Assert.Contains("repo", started.Error.Message, StringComparison.Ordinal);
        Assert.Null(runtime.Started);
        Assert.False(runtime.Asked);
    }

    private static StartInstanceRequest Request(string scopeInput) =>
        new("workflow.checked", "T-1", NextAttempt: false, new Dictionary<FactKey, StartFact>
        {
            [_item] = new("item.json", Encoding.UTF8.GetBytes("{\"id\":\"T-1\"}")),
            [_scopeInput] = new("scope.json", Encoding.UTF8.GetBytes(scopeInput)),
        });

    private static (StartInstanceService Service, Capturing Runtime) Service(bool withCheck = true)
    {
        var runtime = new Capturing();
        IStartFactCheck[] checks = withCheck ? [new ScopeInputStartCheck()] : [];
        var facts = new StartFacts(new InMemoryFactStore(), TimeProvider.System, new StepInputRuntimes([]), callers: null, checks);
        return (new StartInstanceService(new OneItem(), new OneRecipe(), runtime, facts, runtime, new NoLaunches(),
            new ClaimHolders(new InMemoryWorkflowClaims(), new FakeInstanceLiveness())), runtime);
    }

    /// <summary>Records whether the start got as far as asking for the item's attempts (the first write-side step) or starting.</summary>
    private sealed class Capturing : IInstanceRuntime, IInstanceDirectory
    {
        public InstanceStart? Started { get; private set; }

        public bool Asked { get; private set; }

        public ValueTask<Result<AttemptState?>> Latest(ItemKey item, RecipeId recipe, CancellationToken cancellationToken)
        {
            Asked = true;
            return ValueTask.FromResult<Result<AttemptState?>>((AttemptState?)null);
        }

        public ValueTask<Result<InstanceAddress>> Resolve(string reference, CancellationToken cancellationToken) => throw new NotSupportedException();

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

    /// <summary>The ended instance's stored input content: its item and a scope input without its repo.</summary>
    private sealed class Stored : IFactContents
    {
        public ValueTask<Result<ReadOnlyMemory<byte>>> Read(FactValue fact, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success((ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(fact.Sha256 == ItemSha ? "{\"id\":\"T-1\"}" : NoRepo)));
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
        private static readonly RecipeId _id = RecipeId.From("workflow.checked");
        private readonly ApprovedRecipe _recipe = new(
            new RecipeVersion(_id, 1, RegistryData.Hash("checked")),
            ARecipe.Minimal(_id) with { Inputs = [.. ARecipe.Minimal(_id).Inputs, new RecipeInput(FactKey.From("fact:scopeInput"), Label: null, Type: "payload", Kind: "payload", Generate: null)] });

        public ValueTask<Result<ApprovedRecipe>> Latest(RecipeId id, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovedRecipe>>(_recipe);

        public ValueTask<Result<ApprovedRecipe>> Find(RecipeId id, RecipeHash hash, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovedRecipe>>(_recipe);

        public ValueTask<Result<ApprovalCheck>> Check(RecipeVersion recipe, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovalCheck>>(ApprovalCheck.Holds);
    }
}
