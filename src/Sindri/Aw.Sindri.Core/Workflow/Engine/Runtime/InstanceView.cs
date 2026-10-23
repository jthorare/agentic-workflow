using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// What the 'GetInstance' Query returns (engine spec rev 8 §5.1 "Queries", §6.3 'GET /workflow/instances/{id}'): the
/// state, the rendered 'next', 'allowedCommands' as '{ actionId, cli }' (UI E-13), the open question or desk task with
/// the Step's declared outcomes and outputs, the run token and the children. 'workflow status' renders it (§6.4).
/// </summary>
/// <param name="Address">The instance address (its workflow id).</param>
/// <param name="Recipe">The recipe it runs.</param>
/// <param name="State">The state status shows: a recipe state, or 'parked', 'escalated', 'taken-over', 'starting'.</param>
/// <param name="Detail">What follows the state in the header ('desk task', a park reason), when there is one.</param>
/// <param name="Since">When the instance entered <see cref="State"/>.</param>
/// <param name="Next">The rendered 'next' text.</param>
/// <param name="AllowedCommands">The commands it accepts now, the instance's own verbs first (§6.4 order).</param>
/// <param name="Question">The open park, escalation or takeover.</param>
/// <param name="Task">The open desk task.</param>
/// <param name="Run">The open run's token.</param>
/// <param name="Children">The live children.</param>
/// <param name="Outcome">The terminal outcome, once it ended.</param>
public sealed record InstanceView(
    InstanceAddress Address,
    RecipeVersion Recipe,
    string State,
    string? Detail,
    DateTimeOffset Since,
    string Next,
    IReadOnlyList<AllowedCommand> AllowedCommands,
    OpenQuestion? Question,
    DeskTask? Task,
    RunToken? Run,
    IReadOnlyList<ChildView> Children,
    OutcomeName? Outcome)
{
    /// <summary>
    /// The instance's opaque id (spec §6.3), minted from <see cref="Address"/> with <see cref="WorkflowInstanceId.Of"/>:
    /// what the API's instance routes take. Required, so a view read back from JSON always carries it (ARCH-ID-002).
    /// </summary>
    public required WorkflowInstanceId Id { get; init; }

    /// <summary>
    /// The instance's version, 'v&lt;n&gt;' in the status header: the count of events the interpreter has mirrored into the
    /// ledger (the start and every transition). Temporal keeps no row version, so the mirror count stands in for it.
    /// </summary>
    public int Version { get; init; }

    /// <summary>The suggested answer line for a park or escalation (§6.4, one per park reason); null when none applies.</summary>
    public string? Answer { get; init; }

    /// <summary>A drift recorded behind the open park (§6.4 'drift'): the block, and that the answer needs the re-approval first.</summary>
    public string? Drift { get; init; }

    /// <summary>The token budgets of the active state and the instance, when the recipe declares one (§6.4 'budget').</summary>
    public BudgetView? Budget { get; init; }

    /// <summary>What was left of the state's timeout when a takeover paused it (§6.4 'paused'; engine P5 sets it).</summary>
    public TimeSpan? Paused { get; init; }

    /// <summary>Who took the instance over (the takeover suspension's actor); null when it is not taken over.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TakenOverBy { get; init; }

    /// <summary>The open run when an agent session runs it (its token and the launch that proves it); null for a desk or block run. What authorizes a session's report (API spec §4.2).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Instances.OpenRun? AgentRun { get; init; }

    /// <summary>The runs of non-desk steps now executing, which a cancel stops (the cancel confirmation counts them; a desk run is not a running step).</summary>
    public int RunningSteps { get; init; }

    /// <summary>The active state's step declaration (API spec §3.8 'current.step'); null in a terminal state.</summary>
    public StepDeclaration? Step { get; init; }

    /// <summary>The gates on the active state's exits.</summary>
    public IReadOnlyList<GateLine> Gates { get; init; } = [];

    /// <summary>The desk form of the open task, if one is open.</summary>
    public DeskForm? Desk { get; init; }

    /// <summary>The item's id ('ITM-...') when the instance is an item's; null otherwise (UI screens E-20).</summary>
    public ItemKey? ItemId { get; init; }

    /// <summary>The parent instance's id; null for a root. The detail view resolves it to a parent reference.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkflowInstanceId? ParentId { get; init; }

    /// <summary>The SHA-256 of each stored fact, by fact key ('fact:spec'); what the facts route reads the content by. Null when the instance holds none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? FactShas { get; init; }

    /// <summary>How soon the recipe's instances need a human (E-21); work unless the recipe says so.</summary>
    public RecipeUrgency Urgency { get; init; } = RecipeUrgency.Work;

    /// <summary>The badge that replaces the state's glyph when the engine interrupted the instance; null otherwise.</summary>
    public EngineOwnedBadge? EngineOwned { get; init; }

    /// <summary>What the instance is about, from the recipe's first input; null only before the workflow has named it (a view of a starting instance).</summary>
    public SubjectRef? Subject { get; init; }

    /// <summary>The instance's rendered name (spec section 3.11) when the recipe declares one, else null: the first labelled line of status, and the subject's label.</summary>
    public string? Name { get; init; }

    /// <summary>The recipe's inputs, in declaration order.</summary>
    public IReadOnlyList<InputPresence> Inputs { get; init; } = [];

    /// <summary>How <see cref="States"/> is ordered: the path, or the transition list when the graph has several initial paths.</summary>
    public PathOrdering OrderedBy { get; init; } = PathOrdering.Path;

    /// <summary>Every state of the recipe, in <see cref="OrderedBy"/> order.</summary>
    public IReadOnlyList<StateCell> States { get; init; } = [];

    /// <summary>The answer the open park or escalation takes, as the status record's field; null when none applies. Time-independent, so the view can carry it.</summary>
    public StatusAnswer? AnswerLine { get; init; }

    /// <summary>The first live child a park or escalation mirrors, as the status record's field.</summary>
    public StatusChild? ChildPark { get; init; }

    /// <summary>The drift recorded behind the open park, as the status record's field.</summary>
    public StatusDrift? DriftNote { get; init; }

    /// <summary>The re-approval a drift or a revoked child waits for: the protected 'approve' command row.</summary>
    public StatusCommand? ApproveRow { get; init; }

    /// <summary>What the active state asks of a person, as a short lower-case clause ('import the design output'); the list row's 'with you: ...' names it.</summary>
    public string? Activity { get; init; }

    /// <summary>One row per input and output the recipe declares, in declaration order.</summary>
    public IReadOnlyList<FactRow> Facts { get; init; } = [];

    /// <summary>When the instance started; null for one that began before the start was stamped.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Who started the instance, in the actor form of the step runs ('human:prover'); null when nobody is recorded (a child, an automated start, an older instance).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StartedBy { get; init; }

    /// <summary>The instance this one reruns (its address); null for any other. Recorded at start, so it survives a restart.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InstanceAddress? Again { get; init; }

    /// <summary>What each Step state reads and produces (the detail's 'steps'), in the recipe's state order.</summary>
    public IReadOnlyList<StateStep> Steps { get; init; } = [];

    /// <summary>The children, one group per state that declares child or runs step.children; empty until engine P7.</summary>
    public IReadOnlyList<ChildGroup> ChildGroups { get; init; } = [];
}
