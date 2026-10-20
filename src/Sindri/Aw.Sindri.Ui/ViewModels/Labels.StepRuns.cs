using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): a run's Step attempts.</summary>
internal static partial class Labels
{
    /// <summary>The heading of the closed list of runs.</summary>
    internal const string RunsHeading = "Runs";

    /// <summary>Who is on a run that is still open, when it is you.</summary>
    internal const string RunWithYou = "with you";

    /// <summary>Who is on a run that is still open, when it is an agent.</summary>
    internal const string RunWithAgent = "with an agent";

    /// <summary>Who ran a closed run, when it was you.</summary>
    internal const string RunByYou = "by you";

    /// <summary>Who ran a closed run, when it was an agent.</summary>
    internal const string RunByAgent = "by an agent";

    /// <summary>Which try a run is.</summary>
    /// <param name="attempt">The attempt, from 1.</param>
    /// <param name="max">The loop cap, or null when the Step is in no loop.</param>
    /// <returns>"Attempt 2 of 3" or "Attempt 2".</returns>
    internal static string AttemptWords(int attempt, int? max) =>
        max is { } cap ? string.Create(CultureInfo.InvariantCulture, $"Attempt {attempt} of {cap}") : string.Create(CultureInfo.InvariantCulture, $"Attempt {attempt}");

    /// <summary>A served id or state as words: separators become spaces, first letter capital.</summary>
    /// <param name="served">The served word.</param>
    /// <returns>The words.</returns>
    internal static string ReadableWord(string served)
    {
        var spaced = served.Replace('-', ' ').Replace('_', ' ').Replace('.', ' ').Trim();
        return spaced.Length == 0 ? served : string.Concat(char.ToUpperInvariant(spaced[0]).ToString(), spaced.AsSpan(1));
    }

    /// <summary>A run's state in a plain word.</summary>
    /// <param name="state">running, passed, failed, refused, cancelled or abandoned; anything else is made readable.</param>
    /// <returns>The word.</returns>
    internal static string RunStateWord(string state) => state switch
    {
        "running" => "Running",
        "passed" => "Passed",
        "failed" => "Failed",
        "refused" => "Refused",
        "cancelled" => "Cancelled",
        "abandoned" => "Abandoned",
        _ => ReadableWord(state),
    };

    /// <summary>How long a run took.</summary>
    /// <param name="milliseconds">The served duration.</param>
    /// <returns>"under a minute", "4 min" or "1 h 15 min".</returns>
    internal static string RunDuration(long milliseconds)
    {
        var minutes = (int)(Math.Max(0, milliseconds) / 60_000);
        return minutes switch
        {
            0 => "under a minute",
            < 60 => string.Create(CultureInfo.InvariantCulture, $"{minutes} min"),
            _ when minutes % 60 == 0 => string.Create(CultureInfo.InvariantCulture, $"{minutes / 60} h"),
            _ => string.Create(CultureInfo.InvariantCulture, $"{minutes / 60} h {minutes % 60} min"),
        };
    }
}
