namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the decision sheet's who-and-when line and what a decision that was answered reads like. Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The refusal of a Decide whose decision was answered already, before the answer is read.</summary>
    internal const string AlreadyDecidedBare = "Already decided.";

    /// <summary>How long ago, in a few words.</summary>
    /// <param name="age">How long ago it was.</param>
    /// <returns>"just now", "3m ago", "2h ago" or "3d ago".</returns>
    internal static string AgoWords(TimeSpan age) => age.TotalMinutes < 1 ? "just now" : RowAge(age) + " ago";

    /// <summary>Who filed a request, when and for which item; a part that is not known is left out.</summary>
    /// <param name="filedBy">The served actor ("session:cli"), or empty.</param>
    /// <param name="age">How long ago it was filed, or null.</param>
    /// <param name="item">The item it concerns, or null.</param>
    /// <returns>"Filed by an agent session · 26m ago · on ITM-1".</returns>
    internal static string DecisionFiled(string filedBy, TimeSpan? age, string? item)
    {
        var parts = new List<string> { filedBy.Length > 0 ? "Filed by " + StartedByWords(filedBy) : "Filed" };
        if (age is { } ago)
        {
            parts.Add(AgoWords(ago));
        }

        if (!string.IsNullOrEmpty(item))
        {
            parts.Add("on " + item);
        }

        return string.Join(" · ", parts);
    }

    /// <summary>What a decision that was answered reads like, for a person who did not answer it just now.</summary>
    /// <param name="choiceId">The option chosen ("accept").</param>
    /// <param name="choiceLabel">The option's label ("Accept").</param>
    /// <param name="decidedBy">The served actor ("human:cli").</param>
    /// <param name="age">How long ago, or null.</param>
    /// <returns>"accepted by you at the terminal, 3m ago".</returns>
    internal static string DecidedWords(string choiceId, string choiceLabel, string decidedBy, TimeSpan? age)
    {
        var choice = choiceId switch
        {
            "accept" => "accepted",
            "decline" => "declined",
            _ => choiceLabel.ToLowerInvariant(),
        };
        var at = decidedBy.StartsWith("human:", StringComparison.Ordinal) && !decidedBy.EndsWith(":ui", StringComparison.Ordinal) ? " at the terminal" : string.Empty;
        return $"{choice} by {StartedByWords(decidedBy)}{at}" + (age is { } ago ? ", " + AgoWords(ago) : string.Empty);
    }

    /// <summary>The refusal of a Decide whose decision was answered already.</summary>
    /// <param name="words">How it was answered (<see cref="DecidedWords"/>).</param>
    /// <returns>"Already decided: accepted by you at the terminal, 3m ago".</returns>
    internal static string AlreadyDecided(string words) => "Already decided: " + words;
}
