using System.Diagnostics;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Views;
using ActionClass = Aw.Sindri.Core.Commands.ActionClass;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The answer an open park or escalation takes (engine spec section 5.8, API spec section 3.8): one rule, shared by the status
/// layout (<see cref="InstanceWords"/>) and the needs-you rows, so a row's control is the one the status prints. The needs-you fold has no
/// recipe, so for a budget park it passes a placeholder token grant: the row's kind and action id are right, its amount is not the half-budget the status shows.
/// </summary>
public static class ParkAnswers
{
    /// <summary>The answer a suspension takes.</summary>
    /// <param name="shape">The suspension's kind, reason, park kind and escalation.</param>
    /// <param name="address">The instance's address.</param>
    /// <param name="grantTokens">The token amount a budget park's grant line offers.</param>
    /// <param name="child">The mirrored child's address, when known: a pointer line (`via: "child"`) names it.</param>
    /// <returns>The answer, or null when the suspension takes none (a drift, a revoked child or a takeover).</returns>
    public static StatusAnswer? For(ParkShape shape, InstanceAddress address, string grantTokens = "<n>", string? child = null)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var answering = $"sindri workflow answer {address.Value}";
        return shape switch
        {
            { Kind: SuspensionKind.Park, Mirrors: true } when !AnswerVariants.ReasonFixesChildFit(shape.Reason) => Pointer(answering, child),
            { Kind: SuspensionKind.Park, Reason: ParkReason.SpecConflict, ParkKind: ParkKind.Drift or ParkKind.ChildRevoked } => null,
            { Kind: SuspensionKind.Park, Reason: ParkReason.SpecConflict, ParkKind: ParkKind.Revoked } => Line(AnswerKind.CancelOnly, $"sindri workflow cancel {address.Value}"),
            { Kind: SuspensionKind.Park } => Park(shape, answering, grantTokens),
            { Kind: SuspensionKind.Escalation, Escalation: EscalationKind.Gate } => Line(AnswerKind.Accept, $"{answering} --accept", atTerminal: true),
            { Kind: SuspensionKind.Escalation, Escalation: EscalationKind.Timeout } => Line(AnswerKind.Resume, $"{answering} --resume"),
            { Kind: SuspensionKind.Escalation } => Pointer(answering, child),
            _ => null,
        };
    }

    /// <summary>The line for a suspension the child decides: the cli is a pointer to it, marked `via: "child"` so a client never reads the text.</summary>
    private static StatusAnswer Pointer(string answering, string? child) => Line(AnswerKind.Text, $"{answering} <the child's answer>") with { Via = "child", Child = child };

    private static StatusAnswer Park(ParkShape park, string answering, string grantTokens) => park.Reason switch
    {
        ParkReason.Rounds or ParkReason.NoProgress => Line(AnswerKind.Grant, $"{answering} --grant 1", atTerminal: true),
        ParkReason.Budget => Line(AnswerKind.GrantTokens, $"{answering} --grant-tokens {grantTokens}", atTerminal: true),
        ParkReason.NeedsAnswer when park.ParkKind == ParkKind.Wait => Line(AnswerKind.Resume, $"{answering} --resume [--text <answer>]"),
        ParkReason.NeedsAnswer => Line(AnswerKind.Text, $"{answering} --text <answer>"),
        ParkReason.Timeout or ParkReason.SpecConflict or ParkReason.NoRuntime or ParkReason.Launch or null or _ => Line(AnswerKind.Resume, $"{answering} --resume"),
    };

    private static StatusAnswer Line(AnswerKind kind, string cli, bool atTerminal = false)
    {
        if (Variant(kind) is not { } variant)
        {
            return new(kind, ActionId.From("ApplyCommand:cancel"), ActionClass.Desk, cli, atTerminal);
        }

        var row = AnswerRow.For(variant);
        return new(kind, ActionId.From(row.Action), row.Class, cli, atTerminal);
    }

    private static AnswerVariant? Variant(AnswerKind kind) => kind switch
    {
        AnswerKind.Text => AnswerVariant.Text,
        AnswerKind.Resume => AnswerVariant.Resume,
        AnswerKind.Grant => AnswerVariant.Grant,
        AnswerKind.GrantTokens => AnswerVariant.GrantTokens,
        AnswerKind.Accept => AnswerVariant.Accept,
        AnswerKind.CancelOnly => null,
        _ => throw new UnreachableException($"no answer variant for {kind}"),
    };
}
