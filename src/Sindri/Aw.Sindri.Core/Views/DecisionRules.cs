using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Decisions;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.Views;

/// <summary>The pure rules of deciding (API spec §3.2 'Decide', §9).</summary>
public static class DecisionRules
{
    /// <summary>The longest note, in Unicode scalars (P4 plan G2).</summary>
    public const int NoteMax = 64 * 1024;

    /// <summary>Settles <paramref name="decision"/> with <paramref name="choice"/>.</summary>
    /// <param name="decision">The decision as the views hold it.</param>
    /// <param name="choice">The option chosen.</param>
    /// <param name="note">The note given with it, or null.</param>
    /// <param name="actor">Who decides, as the envelope's actor reads.</param>
    /// <param name="command">The 'Decide' command's id.</param>
    /// <returns>The 'decision.recorded' payload; SND-ITEM-409 when it was decided elsewhere or its item was closed; SND-DEC-001 when the choice isn't an option or the note is too long.</returns>
    public static Result<DecisionRecorded> Decide(HumanDecisionRow decision, DecisionOption choice, string? note, string actor, CommandId command)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(actor);
        switch (decision.State)
        {
            case DecisionState.Decided:
                return SindriErrors.ItemConflict.With($"{decision.Id.Value} was decided elsewhere ({decision.Cleared ?? "already decided"})");
            case DecisionState.Voided:
                return SindriErrors.ItemConflict.With($"{decision.Id.Value}: {EmptyText.Voided()}");
            case DecisionState.Pending:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(decision), decision.State, "not a decision state");
        }

        var options = DecisionKinds.Options(decision.Kind);
        if (!options.Any(o => o.Id == choice))
        {
            return SindriErrors.DecisionInvalid.With(
                $"'{SafeText.Escape(choice.Value)}' isn't an option of {decision.Id.Value} (fix: choose {string.Join(" or ", options.Select(static o => o.Id.Value))})");
        }

        if (note is not null && Scalars.Count(note) > NoteMax)
        {
            return SindriErrors.DecisionInvalid.With(string.Create(CultureInfo.InvariantCulture, $"The note is longer than {NoteMax} characters (fix: shorten it)"));
        }

        return new DecisionRecorded(command, decision.Id, choice, note, actor, decision.Repo);
    }
}
