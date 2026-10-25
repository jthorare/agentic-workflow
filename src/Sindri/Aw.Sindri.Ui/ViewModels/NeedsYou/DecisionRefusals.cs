using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>How a refused Decide reads, for the decision sheet and the item sheet alike: only "decided elsewhere" is put in words; a voided decision or a closed item keeps the sentence the server served.</summary>
internal static class DecisionRefusals
{
    /// <summary>The refusal code of a decision that changed under the person.</summary>
    internal const string Conflict = "SND-ITEM-409";

    private const string DecidedElsewhere = "decided elsewhere";

    /// <summary>True when the refusal says the decision was answered elsewhere.</summary>
    /// <param name="code">The served code.</param>
    /// <param name="message">The served detail.</param>
    /// <returns>True for the 409 of an answered decision.</returns>
    internal static bool IsDecidedElsewhere(string code, string message) =>
        string.Equals(code, Conflict, StringComparison.Ordinal) && message.Contains(DecidedElsewhere, StringComparison.Ordinal);

    /// <summary>How an answered decision reads: what was chosen, by whom (the app's words for the actor) and how long ago.</summary>
    /// <param name="row">The decision from the decided list.</param>
    /// <param name="clock">Now.</param>
    /// <returns>"accepted by you at the terminal, 3m ago".</returns>
    internal static string Words(DecisionView row, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(clock);
        var label = row.Options.FirstOrDefault(o => string.Equals(o.Id, row.Choice, StringComparison.Ordinal))?.Label ?? row.Choice ?? "";
        return Labels.DecidedWords(row.Choice ?? "", label, row.DecidedBy ?? "", row.DecidedAt is { } at ? clock.GetUtcNow() - at : null);
    }

    /// <summary>The refusal line once the decided list is read: the answer in words, a voided decision's served line, or empty when the list does not hold it.</summary>
    /// <param name="row">The decision from the decided list, or null.</param>
    /// <param name="clock">Now.</param>
    /// <returns>The line.</returns>
    internal static string Explained(DecisionView? row, TimeProvider clock) =>
        row is null ? "" : row.DecidedBy is { Length: > 0 } ? Labels.AlreadyDecided(Words(row, clock)) : row.Cleared ?? "";
}
