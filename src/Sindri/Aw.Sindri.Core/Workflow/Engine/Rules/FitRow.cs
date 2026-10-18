using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>One row of spec §3.8 as the answer sees it: how the suspension is shown and the fix (the §6.4 answer line); which variants fit is <see cref="AnswerVariants.Fitting"/>.</summary>
/// <param name="Shown">The suspension as status shows it ('parked (rounds)').</param>
/// <param name="Fix">The command that fits.</param>
internal sealed record FitRow(string Shown, string Fix)
{
    /// <summary>The row for <paramref name="suspension"/>.</summary>
    /// <param name="suspension">The park or escalation (not a mirror).</param>
    /// <param name="address">The instance's address.</param>
    /// <param name="recipe">The recipe it runs.</param>
    /// <returns>The row.</returns>
    public static FitRow For(Suspension suspension, string address, PinnedRecipe recipe)
        => suspension.Kind == SuspensionKind.Escalation ? Escalated(suspension, address) : Parked(suspension, address, recipe);

    private static FitRow Parked(Suspension suspension, string address, PinnedRecipe recipe) => suspension.Reason switch
    {
        ParkReason.Rounds => new("parked (rounds)", FitRefusals.Answer(address, "--grant 1")),
        ParkReason.NoProgress => new("parked (no-progress)", FitRefusals.Answer(address, "--grant 1")),
        ParkReason.Budget => new("parked (budget)", FitRefusals.Answer(address, "--grant-tokens <n>")),
        ParkReason.NeedsAnswer => NeedsAnswer(suspension, address),
        ParkReason.Timeout => new("parked (timeout)", FitRefusals.Answer(address, "--resume")),
        ParkReason.Launch => new("parked (launch)", FitRefusals.Answer(address, "--resume") + " once the cause is fixed"),
        ParkReason.NoRuntime => new("parked (SND-WF-010)", FitRefusals.Answer(address, "--resume") + " in a build that has the runtime"),
        ParkReason.SpecConflict or _ => SpecConflict(suspension, address, recipe),
    };

    // From a Step, '--text' re-runs it with the answer; from a wait, '--resume [--text]' checks the gates again.
    private static FitRow NeedsAnswer(Suspension suspension, string address) => suspension.ParkKind == ParkKind.Wait
        ? new("parked (needs-answer)", FitRefusals.Answer(address, "--resume [--text <answer>]"))
        : new("parked (needs-answer)", FitRefusals.Answer(address, "--text <answer>"));

    private static FitRow SpecConflict(Suspension suspension, string address, PinnedRecipe recipe)
    {
        var approve = $"sindri recipe approve {recipe.Id.Value}@{recipe.Hash.Display}";
        return suspension.ParkKind switch
        {
            ParkKind.Dispatch => new("parked (spec-conflict: dispatch)",
                "add the effect to the profile's allowlist and approve the profile, then " + FitRefusals.Answer(address, "--resume")),
            ParkKind.Drift => new("parked (spec-conflict: drift)", approve + "; the approval resumes it"),
            ParkKind.ChildRevoked => new("parked (spec-conflict: child revoked)", approve + " so it pins an approved child; the approval resumes it"),
            ParkKind.Revoked or ParkKind.Loop or ParkKind.Refusal or ParkKind.Step or ParkKind.Wait or ParkKind.Unconfinable or ParkKind.SelfCheck or null or _ => new("parked (spec-conflict: revoked)", $"sindri workflow cancel {address}, then start a new attempt from an approved version"),
        };
    }

    private static FitRow Escalated(Suspension suspension, string address) => suspension.Escalation switch
    {
        EscalationKind.Gate => new("escalated (gate)", FitRefusals.Answer(address, "--accept") + " or --reject"),
        EscalationKind.Timeout => new("escalated (timeout)", FitRefusals.Answer(address, "--resume")),
        EscalationKind.Child or null or _ => new("escalated", FitRefusals.Status(address)),
    };
}
