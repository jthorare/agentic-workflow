using System.Text.Json;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>
/// The engine's own notices (engine spec §5.7 "origin decides the exemption", §6.3; engine P6 unit (c)): a park, an
/// escalation, takeoverIdle or a desk task each emits one 'effect.notify.desk' with origin 'engine', exempt from the
/// recheck, signalling nothing. Its identity is '{instance id}:{entry}:n{version}' (<see cref="EffectIdentity.ForNotice"/>),
/// so two parks in one entry get distinct identities.
/// </summary>
public static class EngineNotices
{
    private static readonly BlockId _notify = BlockId.From("effect.notify.desk");

    /// <summary>The notices a change from <paramref name="before"/> to <paramref name="after"/> sends, with their class.</summary>
    /// <param name="before">The instance before the change (null at start).</param>
    /// <param name="after">The instance after it.</param>
    /// <param name="version">The instance's version after the change ('v&lt;n&gt;', the mirrored event count).</param>
    /// <param name="deskTaskNotified">Whether the instance already sent a desk-task notice (only the first interrupts).</param>
    /// <returns>Each notice's command and class.</returns>
    public static IReadOnlyList<(EffectCommand Command, NoticeClass Class)> For(WorkflowInstance? before, WorkflowInstance after, int version, bool deskTaskNotified) =>
        For(before, after, version, deskTaskNotified, new NoticeChange(WorkflowEventType.Transition));

    /// <summary>The notices a change of type <paramref name="change"/> sends, naming each stage by its id made readable.</summary>
    /// <param name="before">The instance before the change (null at start).</param>
    /// <param name="after">The instance after it.</param>
    /// <param name="version">The instance's version after the change.</param>
    /// <param name="deskTaskNotified">Whether the instance already sent a desk-task notice.</param>
    /// <param name="change">The type of the change's ledger event.</param>
    /// <returns>Each notice's command and class.</returns>
    public static IReadOnlyList<(EffectCommand Command, NoticeClass Class)> For(WorkflowInstance? before, WorkflowInstance after, int version, bool deskTaskNotified, WorkflowEventType change) =>
        For(before, after, version, deskTaskNotified, new NoticeChange(change));

    /// <summary>
    /// The notices a change of type <paramref name="change"/> sends (§6.3): a park or an escalation that is new
    /// interrupts; a 'workflow.notified' change of a taken-over instance is takeoverIdle's (P5) and interrupts; a desk
    /// task newly open interrupts if it is the instance's first and is info otherwise. A takeover, a resume into the same
    /// run and an ended instance send nothing.
    /// </summary>
    /// <param name="before">The instance before the change (null at start).</param>
    /// <param name="after">The instance after it.</param>
    /// <param name="version">The instance's version after the change.</param>
    /// <param name="deskTaskNotified">Whether the instance already sent a desk-task notice.</param>
    /// <param name="change">The change's ledger event type and how its banner names a stage.</param>
    /// <returns>Each notice's command and class.</returns>
    public static IReadOnlyList<(EffectCommand Command, NoticeClass Class)> For(WorkflowInstance? before, WorkflowInstance after, int version, bool deskTaskNotified, NoticeChange change)
    {
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(change);
        if (after.Ended)
        {
            return [];
        }

        var stage = NoticeWords.Stage(after.Frame.State, change.StageName);
        var notice = after.Suspension switch
        {
            { Kind: SuspensionKind.Takeover } when change.Type == WorkflowEventType.Notified =>
                (NoticeClass.Interrupt, NoticeWords.IdleTakeover(stage)),
            { Kind: SuspensionKind.Park } park when AsksAHuman(park, before) =>
                (NoticeClass.Interrupt, NoticeWords.Park(stage, park.Reason)),
            { Kind: SuspensionKind.Escalation } escalation when escalation != before?.Suspension =>
                (NoticeClass.Interrupt, NoticeWords.Escalation(stage)),
            null when after.Frame.Run is { Runtime: StepRuntime.Desk } run && run.Token != before?.Frame.Run?.Token =>
                (deskTaskNotified ? NoticeClass.Info : NoticeClass.Interrupt, NoticeWords.DeskTask(stage)),
            _ => ((NoticeClass Class, string Text)?)null,
        };
        if (notice is not { } found)
        {
            return [];
        }

        var (kind, text) = found;
        var row = after.Suspension switch
        {
            { Kind: SuspensionKind.Takeover } => "takeover",
            { Kind: SuspensionKind.Park or SuspensionKind.Escalation } => "park",
            _ => "desk",
        };
        var with = new Dictionary<string, WithValue>(StringComparer.Ordinal)
        {
            ["class"] = Literal(kind == NoticeClass.Interrupt ? "interrupt" : "info"),
            ["text"] = Literal(text),
            ["row"] = Literal(row),   // which needs-you row the notice is about: 'effect.notify.desk' builds its id (NeedsYouRowId) from this and the instance
        };
        return [(new EffectCommand(_notify, EffectIdentity.ForNotice(after.Id, after.Frame.Entry, version), CommandOrigin.Engine, with), kind)];
    }

    // A new park notifies, except one for a dependency that is down: it asks nothing of a human, and health shows it.
    private static bool AsksAHuman(Suspension park, WorkflowInstance? before) =>
        park != before?.Suspension && park.Reason != ParkReason.Unavailable;

    /// <summary>Whether a change leaving the instance as <paramref name="after"/> sent a desk-task notice (the first one interrupts).</summary>
    /// <param name="after">The instance after the change.</param>
    /// <param name="notices">The notices <see cref="For(WorkflowInstance?, WorkflowInstance, int, bool, NoticeChange)"/> returned for it.</param>
    /// <returns>True when one of them is the desk task's.</returns>
    public static bool NoticedDeskTask(WorkflowInstance after, IReadOnlyList<(EffectCommand Command, NoticeClass Class)> notices)
    {
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(notices);
        return notices.Count > 0 && after.Suspension is null && after.Frame.Run is { Runtime: StepRuntime.Desk };
    }

    private static WithValue Literal(string text)
    {
        using var document = JsonDocument.Parse("\"" + JsonEncodedText.Encode(text).Value + "\"");
        return new WithValue(document.RootElement.Clone(), Fact: null, Profile: null);
    }

    /// <summary>The word a park reason goes by in a notice and in a line's phase event.</summary>
    /// <param name="reason">The reason, or null.</param>
    /// <returns>The reason's word.</returns>
    internal static string Reason(ParkReason? reason) => reason switch
    {
        ParkReason.Rounds => "rounds",
        ParkReason.NoProgress => "no-progress",
        ParkReason.Budget => "budget",
        ParkReason.NeedsAnswer => "needs-answer",
        ParkReason.Timeout => "timeout",
        ParkReason.SpecConflict => "spec-conflict",
        ParkReason.NoRuntime => "SND-WF-010",
        ParkReason.Unavailable => "unavailable",
        _ => "parked",
    };
}
