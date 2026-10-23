using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>
/// The 'park' rows (API spec section 3.6, API P11): an engine park or escalation, an open desk task and an idle takeover, each keyed
/// by the instance's opaque id. The title is the item's (already scrubbed), the reason is words for the park's reason: the ledger
/// holds no question text, so none reaches a row. A dispatch park's row carries the recorded refusal (code and message), which is
/// the effect's own error, not question text.
/// </summary>
public static partial class NeedsYouRows
{
    private static readonly ActionId _completeStep = ActionId.From("CompleteStep");
    private static readonly ActionId _handback = ActionId.From("ApplyCommand:handback");
    private static readonly ActionId _cancel = ActionId.From("ApplyCommand:cancel");
    private static readonly ActionId _approveRecipe = ActionId.From("ApproveRecipe");

    /// <summary>The 'park' row of a root instance's park or escalation, or of its deepest parked descendant (<see cref="ParkRowInput.Descendant"/>).</summary>
    /// <param name="item">The item's row, for its title, repo and id.</param>
    /// <param name="park">The instance, its suspension and the refusal or descendant that shape the row.</param>
    /// <param name="since">When the row appeared.</param>
    /// <param name="scrubber">Scrubs the text the row stores.</param>
    /// <returns>The row.</returns>
    public static NeedsYouRow ForPark(ItemRow item, ParkRowInput park, DateTimeOffset since, ITextScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(park);
        var suspension = park.Suspension;
        var at = park.Descendant ?? park.Address;
        var answer = AnswerOf(park);
        var (label, words) = Words(suspension, park.Refusal);
        var shape = new EngineRow("park:", label, $"{words} ({at.Value})", Urgency.Pulse, answer?.Kind,
            answer?.ActionId ?? _approveRecipe, answer is null ? [_approveRecipe, _cancel] : [answer.ActionId], answer?.Cli ?? Status(at), answer?.Via, answer?.Child);
        return Row(item, park.Address, shape, since, scrubber);
    }

    /// <summary>The answer a park row offers: a pointer to the descendant that decides, or the instance's own.</summary>
    private static Workflow.Views.StatusAnswer? AnswerOf(ParkRowInput park)
    {
        if (park.Descendant is { } descendant)
        {
            return ParkAnswers.For(new ParkShape(SuspensionKind.Escalation, Reason: null, ParkKind: null, EscalationKind.Child, Mirrors: false), descendant, child: descendant.Value);   // the descendant decides: a pointer to it
        }

        var suspension = park.Suspension;
        return ParkAnswers.For(new ParkShape(suspension.Kind, suspension.Reason, suspension.ParkKind, suspension.Escalation, suspension.Mirrors is not null), park.Address, child: suspension.MirrorsAddress);
    }

    /// <summary>The 'park' row of an open desk task.</summary>
    /// <param name="item">The item's row.</param>
    /// <param name="address">The instance's address.</param>
    /// <param name="since">When the row appeared.</param>
    /// <param name="scrubber">Scrubs the text the row stores.</param>
    /// <returns>The row.</returns>
    public static NeedsYouRow ForDeskTask(ItemRow item, InstanceAddress address, DateTimeOffset since, ITextScrubber scrubber) =>
        Row(item, address, new EngineRow("desk:", "desk task", $"a desk task is open ({address.Value})", Urgency.Colour, AnswerKind: null, _completeStep, [_completeStep], Status(address)), since, scrubber);

    /// <summary>The 'park' row of a takeover that went idle.</summary>
    /// <param name="item">The item's row.</param>
    /// <param name="address">The instance's address.</param>
    /// <param name="since">When the row appeared.</param>
    /// <param name="scrubber">Scrubs the text the row stores.</param>
    /// <returns>The row.</returns>
    public static NeedsYouRow ForIdleTakeover(ItemRow item, InstanceAddress address, DateTimeOffset since, ITextScrubber scrubber) =>
        Row(item, address, new EngineRow("takeover:", "taken over", $"a takeover is idle ({address.Value})", Urgency.Calm, AnswerKind: null, _handback, [_handback, _cancel], $"sindri handback {address.Value}"), since, scrubber);

    private static string Status(InstanceAddress address) => $"sindri workflow status {address.Value}";

    private sealed record EngineRow(string Prefix, string Label, string Reason, Urgency Urgency, Workflow.Views.AnswerKind? AnswerKind, ActionId Primary, IReadOnlyList<ActionId> Actions, string Cli, string? Via = null, string? Child = null);

    private static NeedsYouRow Row(ItemRow item, InstanceAddress address, EngineRow shape, DateTimeOffset since, ITextScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(scrubber);
        var instance = WorkflowInstanceId.Of(address).Value;
        return new NeedsYouRow(
            NeedsYouRowId.From(shape.Prefix + instance),
            NeedsYouKind.Park,
            shape.Label,
            new NeedsYouSubject(SubjectKind.Instance, SubjectId.From(instance)),
            item.Id,
            SafeText.Clean(scrubber, item.Title, TitleMax),
            SafeText.Clean(scrubber, shape.Reason, ReasonMax),
            item.Repo,
            shape.Urgency,
            since,
            RecordedOnly: false,
            Notice: null,
            shape.AnswerKind,
            shape.Primary,
            shape.Actions,
            shape.Cli,
            shape.Via,
            shape.Child);
    }

    private static (string Label, string Words) Words(SuspensionSummary s, string? refusal) => s switch
    {
        { Kind: SuspensionKind.Escalation, Escalation: EscalationKind.Gate } => ("escalated · gate", "a gate escalated: accept or reject"),
        { Kind: SuspensionKind.Escalation, Escalation: EscalationKind.Timeout } => ("escalated · timeout", "a timeout escalated"),
        { Kind: SuspensionKind.Escalation } => ("escalated · child", "a child escalated"),
        { Reason: ParkReason.Rounds } => ("park · rounds", "the rounds are used up"),
        { Reason: ParkReason.NoProgress } => ("park · no progress", "the rounds made no progress"),
        { Reason: ParkReason.Budget } => ("park · budget", "the token budget is used up"),
        { Reason: ParkReason.NeedsAnswer } => ("park · needs answer", "it needs an answer"),
        { Reason: ParkReason.Timeout } => ("park · timeout", "it ran past its time"),
        { Reason: ParkReason.SpecConflict, ParkKind: ParkKind.Revoked } => ("park · spec conflict", "its recipe was revoked"),
        { Reason: ParkReason.SpecConflict, ParkKind: ParkKind.ChildRevoked } => ("park · spec conflict", "a child's recipe was revoked"),
        { Reason: ParkReason.SpecConflict, ParkKind: ParkKind.Dispatch } => ("park · dispatch refused", refusal ?? "an effect was refused"),
        { Reason: ParkReason.SpecConflict } => ("park · spec conflict", "its recipe drifted since approval"),
        { Reason: ParkReason.NoRuntime } => ("park · no runtime", "no runtime runs its step"),
        _ => ("park", "it is parked"),
    };
}
