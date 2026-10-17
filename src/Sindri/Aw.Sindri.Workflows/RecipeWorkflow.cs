using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The recipe interpreter: the one workflow type that runs every recipe (engine spec rev 8 §5.1). Its input is
/// <see cref="InstanceStart"/> and its workflow id is the instance address. Workflow code is deterministic: no I/O,
/// clock or random except through 'Workflow.*'; every decision is P3's pure <see cref="TransitionDecision"/>, and it
/// awaits with ConfigureAwait(true) (finding F3).
/// <para>
/// Engine P4. It loads the pinned recipe through 'LoadRecipe', enters the initial state, and loops (unit (a),
/// 'RecipeWorkflow.Loop.cs'): an active state runs its desk Step (a short 'RunStep' activity with the run token in
/// its id, then a wait for the 'complete' Update; rev 8.1 §5.3, unit (c)); a wait state, a park, an escalation or a
/// takeover waits for a delivered event that fits or a command an Update submits. Every trigger is decided at the wait,
/// one at a time; each change is mirrored into the ledger through 'RecordEvent' (§5.6) and the search attributes are
/// set. The other Updates of §5.2 have their final signatures and hand their trigger to the loop through 'Submit':
/// (b) 'answer', 'command', 'cancel'; (c) 'complete' and its validator; (d) the bridge's 'event' signal (§5.4). Engine
/// P5 adds the timer scopes ('TimerScopes', §5.5) and 'takeover', 'handback' and 'open-request'
/// ('RecipeWorkflow.Takeover.cs').
/// </para>
/// </summary>
[Workflow("RecipeWorkflow")]
internal sealed partial class RecipeWorkflow
{
    private const string Desk = "human:desk";
    private const string Timers = "service:workflow.timers";
    private static readonly FactKey _itemFact = FactKey.From("fact:item");
    private readonly Queue<WaitSatisfied> _delivered = new();
    private readonly HashSet<long> _eventsTaken = [];
    private readonly Dictionary<string, Reported> _reported = new(StringComparer.Ordinal);
    private readonly TimerScopes _timers = new(Timers);   // engine P5 (§5.5): unit (a) arms the state timers through it, unit (b) pauses and resumes them
    private InstanceStart? _start;
    private InterpretedRecipe? _recipe;
    private Interpreting? _context;
    private WorkflowInstance? _instance;
    private DateTimeOffset _since;
    private TimeSpan _deskTimeout;
    private string? _repo;
    private SubjectRef? _subject;
    private IReadOnlyDictionary<FactKey, string>? _contents;
    private string? _recorded;
    private StepCompleted? _report;
    private string? _reportId;
    private int _runs;
    private int _mirrored;
    private readonly Dictionary<FactKey, FactProvenance> _provenance = [];

    /// <summary>
    /// Interprets the recipe from its initial state to a terminal one (spec §5.1): an active state runs its Step (unit
    /// (c)'s desk seam, <see cref="RunDeskStep"/>); a wait state, a park, an escalation or a takeover waits
    /// (<see cref="Wait"/>). Each step decides one trigger with P3's pure <see cref="TransitionDecision"/>. Before it
    /// returns it lets the Update that ended the instance finish.
    /// </summary>
    /// <param name="start">The recipe, item, attempt, input facts, tenant and ring.</param>
    /// <returns>The terminal state.</returns>
    [WorkflowRun]
    public async Task<RecipeResult> RunAsync(InstanceStart start)
    {
        var (instance, context) = await Begin(start).ConfigureAwait(true);
        while (!instance.Ended)
        {
            instance = instance switch
            {
                { Suspension: null, Frame.Run: { Runtime: StepRuntime.Block } block } => await RunBlockStep(instance, block, context).ConfigureAwait(true),
                { Suspension: null, Frame.Run: { } open } => await RunDeskStep(instance, open, context).ConfigureAwait(true),
                _ => await Wait(instance, context).ConfigureAwait(true),
            };
        }

        await Settle().ConfigureAwait(true);   // every effect it emitted is dispatched, refused or aborted (§5.7)
        await SettleLeftovers(instance, context).ConfigureAwait(true);
        return new RecipeResult(instance.State, instance.Outcome?.Value);
    }

    /// <summary>
    /// Loads the pinned recipe ('LoadRecipe'), reads the repo 'fact:item' names ('ReadItemRepo'), enters the initial
    /// state with the input's tenant and ring, mirrors 'workflow.started' into the ledger and sets the search attributes.
    /// </summary>
    private async Task<(WorkflowInstance Instance, Interpreting Context)> Begin(InstanceStart start)
    {
        ArgumentNullException.ThrowIfNull(start);
        _start = start;
        _since = Now;
        // DT-J2: first, before any await, so the marker is the first command and replays at the same place; a history that began before the block runtime has no marker, so it replays with no block runtimes, on the desk path exactly as recorded (kept until the cutover; DT-J plan Q7)
        var blockRuntime = Workflow.Patched("dt-j-block-runtime");
        var loaded = await Workflow.ExecuteLocalActivityAsync(
            (RunStepActivities a) => a.LoadRecipeAsync(start.Recipe),
            new LocalActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(30) }).ConfigureAwait(true);
        var catalog = new BlockCatalog(loaded.Blocks, []);
        _recipe = new InterpretedRecipe(loaded.Version, loaded.Recipe, catalog, start.Inputs);
        _deskTimeout = loaded.DeskTimeout;
        _eventHold = loaded.EventHold;
        _dispatchDelay = loaded.DispatchDelay;
        var blocks = blockRuntime ? loaded.BlockRuntimes ?? [] : [];
        var decisions = new DecisionContext(ApprovalCheck.Holds, catalog, new HashSet<BlockId>(), NoChildRecipes.Instance) { DeskTimeout = loaded.DeskTimeout, BlockRuntimes = blocks.ToHashSet() };
        _context = new Interpreting(loaded.Recipe, decisions);
        _repo = start.Inputs.TryGetValue(_itemFact, out var item) ? await RepoOf(item.Value).ConfigureAwait(true) : null;
        (_subject, _contents) = await NamedSubject(_recipe, start).ConfigureAwait(true);
        var change = Started(TransitionDecision.Start(StartOf(start, loaded), loaded.Recipe, decisions));
        var instance = Enter(change.After, Desk);
        foreach (var key in start.Inputs.Where(static i => i.Value.Generated).Select(static i => i.Key))
        {
            _provenance[key] = new FactProvenance(GenerateActor, Now);   // a generated input was made by the engine, not supplied (spec section 3.11)
        }

        await Mirror(new WorkflowEventRecord(start.WorkflowId().Value, EventTypes.WorkflowStarted, start.Item.Value, string.Empty, instance.State, "start", Outcome: null, Run: null, Detail: Named(WorkflowEventDetail.Of(change.Event, trigger: null), start), Causation: start.OpenLaunch), NeedsYou(instance)).ConfigureAwait(true);
        Emit(before: null, instance, []);
        return (instance, _context);
    }

    /// <summary>The instance as status shows it (the 'GetInstance' Query, spec §5.1, §6.3).</summary>
    /// <returns>The state, 'next', 'allowedCommands', the open question or desk task, the run token and the children.</returns>
    [WorkflowQuery("GetInstance")]
    public InstanceView GetInstance()
    {
        var start = _start ?? throw new InvalidOperationException("the instance has no input yet");
        return (_instance, _recipe) is ({ } instance, { } recipe)
            ? InstanceViews.Of(instance, recipe, _since, _timers.FiresAt(), new InstanceViewContext(_mirrored, _provenance, Subject: _subject, Contents: _contents)) with { Id = instance.Id }
            : InstanceViews.Starting(start.WorkflowId(), start.Recipe, _since, start.Item, _subject);
    }

    /// <summary>
    /// A registry change the recipe's approval saw (spec §4.1, W11; engine P6 integration): a revoke parks the instance
    /// at once, a drift parks it, and a drift approval resumes a drift park and re-sends the effect it carries. The loop
    /// decides it at the instance's current wait like any trigger; a repeat is absorbed.
    /// </summary>
    /// <param name="wake">The wake, acting as 'service:engine'.</param>
    /// <returns>Done once queued.</returns>
    [WorkflowSignal("wake")]
    public Task WakeAsync(RecipeWake wake)
    {
        ArgumentNullException.ThrowIfNull(wake);
        _submitted.Enqueue(new Submission(Ticket: 0, wake));
        return Task.CompletedTask;
    }

    /// <summary>
    /// A registered ledger event the bridge routed here (spec §5.4). Temporal buffers it until the instance reaches the
    /// wait; unit (a) consumes <see cref="_delivered"/> at a wait. Unit (d): the event's ledger sequence is its idempotency
    /// key, so a re-sent event (a bridge that crashed before it moved its cursor) is dropped; so is a kind no wait of the
    /// recipe waits for (the bridge signals every live instance of the item). Dropping a buffered one older than 'eventHold' is
    /// that of RecipeWorkflow.Hold.cs (S1-D11's key, unit (e)).
    /// </summary>
    /// <param name="delivered">The event as the wait's trigger.</param>
    /// <returns>Done.</returns>
    [WorkflowSignal("event")]
    public Task EventAsync(WaitSatisfied delivered)
    {
        ArgumentNullException.ThrowIfNull(delivered);
        if (delivered.Source is EventWaitSource e && (!_eventsTaken.Add(e.Sequence) || !WaitsFor(e.Kind)))
        {
            return Task.CompletedTask;
        }

        _delivered.Enqueue(delivered);
        _arrivals++;
        return Task.CompletedTask;
    }

    /// <summary>Whether a wait state of the recipe waits for <paramref name="kind"/>; true while the recipe isn't loaded yet.</summary>
    private bool WaitsFor(EventKind kind) =>
        _context is not { } context || context.Recipe.States.Values.Any(s => s.WaitFor?.Event == kind);

    /// <summary>
    /// Mirrors one event into the ledger ('RecordEvent', a local activity keyed by its own activity id; rev 8.1 §5.6),
    /// then sets the search attributes the instance is found by (§5.1) as they stand after it.
    /// </summary>
    private async Task Mirror(WorkflowEventRecord record, bool needsYou)
    {
        var stamped = record with { Tenant = _start!.Tenant, Ring = _start.Ring };   // why: Mirror runs only after RunAsync set _start; the row is the instance's, not the worker's
        _ = await Workflow.ExecuteLocalActivityAsync((RecordEventActivities a) => a.RecordAsync(stamped), RecordEventActivities.LocalOptions).ConfigureAwait(true);
        _mirrored++;   // the instance's version, 'v<n>' in status: Temporal keeps no row version (unit (e))
        Upsert(record.To, needsYou);
    }

    /// <summary>
    /// The instance waits on a human (spec §5.1 'NeedsYou'): it is parked or escalated, or it holds an open desk task. A
    /// takeover doesn't: the human already has it.
    /// </summary>
    private static bool NeedsYou(WorkflowInstance instance) => !instance.Ended && instance.Suspension switch
    {
        { Kind: SuspensionKind.Takeover } => false,
        not null => true,
        _ => instance.Frame.Run is { Runtime: StepRuntime.Desk },
    };

    /// <summary>
    /// Takes <paramref name="instance"/> as the instance after a decided change, and does what the change asks of the
    /// timers (spec §5.5, <see cref="StateTimers.Between"/>): a new entry or run, or an answer that ended a park, arms the
    /// state's full timeout; a park, escalation or end stops it; a 'then: retry' arms the retry timer; a takeover pauses
    /// it and keeps what was left on its suspension, which the handback re-arms. The first entry arms its state's
    /// timeout unless it parked at once; a start taken over (E-19) keeps the full timeout for the handback. It also
    /// records who produced each fact the change wrote (<paramref name="actor"/>, at the workflow's clock), which the
    /// fact rows show as 'by' and 'at'.
    /// </summary>
    /// <returns>The instance as the interpreter holds it: a takeover's suspension carries what it paused.</returns>
    private WorkflowInstance Enter(WorkflowInstance instance, string actor)
    {
        Stamp(instance, actor);
        var before = _instance;
        if (before is null || !string.Equals(before.State, instance.State, StringComparison.Ordinal) || before.Frame.Entry != instance.Frame.Entry)
        {
            _since = Now;
        }

        var recipe = _context!.Recipe; // why: Begin sets the context before the first entry
        var kept = before is null
            ? First(instance, recipe)
            : _timers.Apply(StateTimers.Between(before, instance, recipe, _deskTimeout), instance.Frame.Entry);
        _instance = kept is not null && instance.Suspension is { Kind: SuspensionKind.Takeover } takeover
            ? instance with { Suspension = takeover with { Remaining = kept } }
            : instance;
        return _instance;
    }

    /// <summary>
    /// The first entry's timers: its state's timeout, unless it ended or parked at once; started taken over (E-19), the
    /// full timeout is kept for the handback and 'takeoverIdle' runs.
    /// </summary>
    private RemainingTimer? First(WorkflowInstance instance, Core.Workflow.Recipes.Recipe recipe)
    {
        var full = instance.Ended ? null : StateTimers.Timeout(recipe.States[instance.Frame.State], instance.Frame.Run, _deskTimeout);
        return (instance.Suspension, full) switch
        {
            ({ Kind: SuspensionKind.Takeover }, _) => _timers.Held(instance.PendingPark is null ? full : null, instance.Frame.Entry),   // parked at once: the handback returns to the park, no timer to resume
            (null, { } after) => _timers.Apply(new TimerAction(TimerActionKind.Arm, DeadlineKind.State, after), instance.Frame.Entry),
            _ => _timers.Apply(TimerAction.Stop, instance.Frame.Entry),
        };
    }

    /// <summary>Records <paramref name="actor"/> and now as the producer of every fact <paramref name="instance"/> holds that the previous state did not, or held with another value.</summary>
    private void Stamp(WorkflowInstance instance, string actor)
    {
        foreach (var (key, value) in instance.Facts)
        {
            if (_instance is null || !_instance.Facts.TryGetValue(key, out var before) || before != value)
            {
                _provenance[key] = new FactProvenance(actor, Now);
            }
        }
    }

    /// <summary>The workflow's deterministic clock, as an offset in UTC.</summary>
    private static DateTimeOffset Now => new(Workflow.UtcNow, TimeSpan.Zero);

    private static StartRequest StartOf(InstanceStart start, LoadedRecipe loaded)
    {
        var address = start.WorkflowId();
        var id = WorkflowInstanceId.Of(address);
        return new StartRequest(
            TriggerKey.For(id, "start"),
            id,
            address,
            new PinnedRecipe(loaded.Version.Id, loaded.Version.Version, loaded.Version.Hash, new Dictionary<RecipeId, RecipeHash>()),
            start.Item,
            start.Attempt,
            Parent: null,
            start.Inputs.ToDictionary(static i => i.Key, static i => i.Value.Value),
            Desk,
            TakenOver: start.OpenLaunch is not null,
            start.Tenant,
            start.Ring);
    }

    private static InstanceChange Started(Decision decision) => decision switch
    {
        Applied applied => applied.Changes[0],
        Rejected rejected => throw new ApplicationFailureException($"the start was rejected: {rejected.Error.Code} {rejected.Error.Message}", errorType: rejected.Error.Code, nonRetryable: true),
        _ => throw new ApplicationFailureException($"the start was {decision.GetType().Name}", nonRetryable: true),
    };

    /// <summary>The instance after a decision: an applied or refused trigger's own change; a rejected or absorbed one leaves it as it was.</summary>
    private static WorkflowInstance After(WorkflowInstance instance, Decision decision) => OwnChange(instance, decision)?.After ?? instance;
}
