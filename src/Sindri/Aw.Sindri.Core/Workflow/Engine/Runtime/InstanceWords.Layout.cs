using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;
using ActionClass = Aw.Sindri.Core.Commands.ActionClass;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The same facts as the words in InstanceWords, as the status record's fields (UI screens E-23, API spec §3.8).</summary>
internal sealed partial record InstanceWords
{
    /// <summary>The badge that replaces the active state's glyph while the engine owns the instance: parked, escalated or taken over, with the reason the header's detail gives.</summary>
    /// <returns>The badge, or null when the instance is running or ended.</returns>
    public EngineOwnedBadge? EngineBadge() => Instance.Suspension is { } suspension && !Instance.Ended
        ? new EngineOwnedBadge(
            suspension.Kind switch
            {
                SuspensionKind.Escalation => EngineOwnedKind.Escalated,
                SuspensionKind.Takeover => EngineOwnedKind.TakenOver,
                SuspensionKind.Park or _ => EngineOwnedKind.Parked,
            },
            suspension.Kind == SuspensionKind.Takeover ? $"taken over by {Taker(suspension.Actor)}" : Detail() ?? suspension.Question)
        : null;

    /// <summary>The answer the open park or escalation takes; null when none (a drift or a revoked child takes no answer, a desk task completes).</summary>
    /// <returns>The kind, the catalog action, its class and the command the answer line prints.</returns>
    public StatusAnswer? AnswerLine() => Instance.Suspension is { } suspension && !Instance.Ended
        ? ParkAnswers.For(new ParkShape(suspension.Kind, suspension.Reason, suspension.ParkKind, suspension.Escalation, suspension.Mirrors is not null), Instance.Address, GrantTokens(), Child?.Address.Value)
        : null;

    /// <summary>A drift recorded behind the open park, as fields: the block, and that the answer needs the re-approval first.</summary>
    /// <returns>The note, or null.</returns>
    public StatusDrift? DriftNote() => BehindDrift() is { } drift
        ? new StatusDrift(BlockId.From(Block(drift.Question)), NeedsReapproval: true)
        : null;

    /// <summary>The first live child, when the park or escalation mirrors one: its address, its state and the reason it is parked.</summary>
    /// <returns>The child, or null.</returns>
    public StatusChild? ChildPark()
    {
        if (Child is not { } child)
        {
            return null;
        }

        var cut = child.State.IndexOf(": ", StringComparison.Ordinal);
        return cut < 0
            ? new StatusChild(WorkflowInstanceId.Of(child.Address), child.Address, child.State, Reason: null)
            : new StatusChild(WorkflowInstanceId.Of(child.Address), child.Address, child.State[..cut], child.State[(cut + 2)..]);
    }

    /// <summary>The re-approval a drift (or a revoked child) waits for: the protected 'approve' command.</summary>
    /// <returns>The row, or null when the park needs no approval.</returns>
    public StatusCommand? ApproveRow() => Instance.Suspension is { Kind: SuspensionKind.Park, Reason: ParkReason.SpecConflict, ParkKind: { } kind }
        ? kind switch
        {
            ParkKind.Drift => new StatusCommand(StatusVerb.Approve, ActionId.From("ApproveRecipe"), ActionClass.Protected, $"sindri recipe approve {Pinned}", Confirm: null),
            ParkKind.ChildRevoked => new StatusCommand(StatusVerb.Approve, ActionId.From("ApproveRecipe"), ActionClass.Protected, $"sindri recipe approve {Recipe.Version.Id.Value}", Confirm: null),
            ParkKind.Revoked or ParkKind.Dispatch or ParkKind.Loop or ParkKind.Refusal or ParkKind.Step or ParkKind.Wait or ParkKind.Unconfinable or ParkKind.SelfCheck or _ => null,
        }
        : null;

    /// <summary>
    /// What the active state asks of a person, as a short clause ('import the design output'): the state's 'next' text up to
    /// its first ', then' or sentence end, with its first letter lower-cased; the state's id when the recipe gives none.
    /// </summary>
    /// <returns>The clause.</returns>
    public string Activity()
    {
        var text = RecipeNext().Trim();
        var end = text.IndexOf(", then ", StringComparison.Ordinal);
        if (end < 0)
        {
            end = text.IndexOf(". ", StringComparison.Ordinal);
        }

        var clause = (end < 0 ? text : text[..end]).TrimEnd('.').Trim();
        return clause.Length == 0
            ? Instance.Frame.State
            : clause.Length > 1 && char.IsUpper(clause[0]) && char.IsLower(clause[1]) ? char.ToLowerInvariant(clause[0]) + clause[1..] : clause;
    }

    /// <summary>Who took the instance over: the actor without its kind ('human:thor' is 'thor').</summary>
    private static string Taker(string actor) => actor[(actor.IndexOf(':', StringComparison.Ordinal) + 1)..];
}
