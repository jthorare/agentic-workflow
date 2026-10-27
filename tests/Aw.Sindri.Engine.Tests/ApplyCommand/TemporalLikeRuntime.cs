using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Tests.ApplyCommand;

/// <summary>
/// One instance behind the runtime port, behaving as Temporal does for an Update id: the first Send with a nonce decides
/// (or is refused) and a repeat of that nonce returns the first outcome. A test can lose the first response (a timeout
/// after the Update applied) or hold a send open to overlap two requests. The instance's version moves with each applied
/// Update, so a challenge issued before it is stale after.
/// </summary>
internal sealed class TemporalLikeRuntime : IInstanceDirectory, IInstanceRuntime, IApprovedRecipes, ICallerContextAccessor
{
    private static readonly RecipeVersion _recipe = new(RecipeId.From("workflow.step"), 1, RecipeHash.From("sha256:" + new string('a', 64)));
    private readonly Dictionary<CommandNonce, Result<CommandResult>> _first = [];
    private readonly List<CommandNonce> _sent = [];
    private TaskCompletionSource? _hold;
    private TaskCompletionSource? _entered;
    private TaskCompletionSource? _held;
    private bool _loseNextResponse;
    private bool _refuseNext;

    public InstanceAddress Address { get; } = InstanceAddress.From("T-1/step/1");

    /// <summary>The child the parent's open park mirrors; null when it mirrors none.</summary>
    public InstanceAddress? Child { get; private set; }

    /// <summary>Every Update a Send handed over, with the instance it went to, in order.</summary>
    public List<(InstanceAddress To, Trigger Trigger)> Delivered { get; } = [];

    /// <summary>The parent's open park now mirrors <paramref name="child"/> (a no-progress park inside it).</summary>
    public void MirrorChild(InstanceAddress child) => Child = child;

    /// <summary>How many Updates the instance applied.</summary>
    public int Applied { get; private set; }

    public int Gets { get; private set; }

    /// <summary>What the approval check answers (Holds unless a test says otherwise).</summary>
    public ApprovalCheck Approval { get; set; } = ApprovalCheck.Holds;

    /// <summary>When set, the instance's Query fails with it (Temporal down, no worker).</summary>
    public Error? GetFails { get; set; }

    /// <summary>When set, the approval check itself fails with it (the approvals can't be read).</summary>
    public Error? CheckFails { get; set; }

    /// <summary>Every nonce a Send used, in order.</summary>
    public IReadOnlyList<CommandNonce> Sent => _sent;

    public CallerContext? Current { get; set; } = Testing.A.Caller().AtTerminal().Build();

    /// <summary>The open desk task the views carry; null for none.</summary>
    public DeskTask? OpenTask { get; set; }

    /// <summary>The open park, escalation or takeover the views carry; null for none.</summary>
    public OpenQuestion? OpenPark { get; set; }

    /// <summary>The open agent run the views carry; null for none.</summary>
    public OpenRun? OpenAgentRun { get; set; }

    /// <summary>The next new Update applies, but its response is lost: the caller sees a timeout.</summary>
    public void LoseNextResponse() => _loseNextResponse = true;

    /// <summary>The next new Update is refused by the validator with SND-WF-003, applying nothing.</summary>
    public void RefuseNext() => _refuseNext = true;

    /// <summary>The next new Update stays open until <see cref="Release"/>; the returned task completes when it arrives.</summary>
    public Task HoldNext()
    {
        _entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _hold = _held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return _entered.Task;
    }

    public void Release() => _held?.SetResult();

    public InstanceView View() => View(version: 4 + Applied);

    public ValueTask<Result<AttemptState?>> Latest(ItemKey item, RecipeId recipe, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<AttemptState?>>((AttemptState?)null);

    public ValueTask<Result<InstanceAddress>> Resolve(string reference, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<InstanceAddress>>(Address);

    public ValueTask<Result<InstanceView>> Start(InstanceStart start, CancellationToken cancellationToken) =>
        throw new NotSupportedException("not started by these tests");

    public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken)
    {
        Gets++;
        return ValueTask.FromResult(GetFails is { } failure ? (Result<InstanceView>)failure : View(address, version: 4 + Applied));
    }

    public async ValueTask<Result<CommandResult>> Send(InstanceAddress address, CommandNonce nonce, Trigger command, CancellationToken cancellationToken)
    {
        _sent.Add(nonce);
        Delivered.Add((address, command));
        if (_first.TryGetValue(nonce, out var known))
        {
            return known;
        }

        var hold = _hold;
        if (hold is not null)
        {
            _hold = null;
            _entered!.SetResult();   // why: HoldNext sets both
            await hold.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        Result<CommandResult> outcome;
        if (_refuseNext)
        {
            _refuseNext = false;
            outcome = WorkflowErrors.TriggerNotAllowed.With("not allowed here");
        }
        else
        {
            Applied++;
            outcome = new CommandResult(View(), "resumed (transition): -> produce");
        }

        _first[nonce] = outcome;
        if (_loseNextResponse && !outcome.IsError)
        {
            _loseNextResponse = false;
            return Core.SindriErrors.ApiTimeout.With("Sindri didn't answer in time");
        }

        return outcome;
    }

    public ValueTask<Result<CommandResult>> OpenRequest(InstanceAddress address, LaunchId launch, CancellationToken cancellationToken) =>
        throw new NotSupportedException("not requested by these tests");

    public ValueTask<Result<Unit>> Signal(InstanceAddress address, WaitSatisfied delivered, CancellationToken cancellationToken) =>
        throw new NotSupportedException("not signalled by these tests");

    public ValueTask<Result<ApprovedRecipe>> Latest(RecipeId id, CancellationToken cancellationToken) =>
        throw new NotSupportedException("not asked by these tests");

    public ValueTask<Result<ApprovedRecipe>> Find(RecipeId id, RecipeHash hash, CancellationToken cancellationToken) =>
        throw new NotSupportedException("not asked by these tests");

    public ValueTask<Result<ApprovalCheck>> Check(RecipeVersion recipe, CancellationToken cancellationToken) =>
        ValueTask.FromResult(CheckFails is { } failure ? (Result<ApprovalCheck>)failure : Approval);

    public IDisposable Enter(CallerContext context) => throw new NotSupportedException("the caller is fixed");

    public IDisposable EnterService(string name) => throw new NotSupportedException("the caller is fixed");

    private InstanceView View(int version) => View(Address, version);

    private InstanceView View(InstanceAddress address, int version) => new(
        address,
        _recipe,
        "parked",
        Detail: null,
        DateTimeOffset.FromUnixTimeMilliseconds(1_000),
        string.Empty,
        [],
        Question: OpenPark,
        Task: OpenTask,
        Run: null,
        Children: [],
        Outcome: null)
    {
        Id = WorkflowInstanceId.Of(address),
        AgentRun = OpenAgentRun,
        Version = version,
        ChildPark = Child is { } child && address == Address ? new StatusChild(WorkflowInstanceId.Of(child), child, "parked", "no-progress") : null,
    };
}
