namespace Aw.Sindri.Features.Prove;

/// <summary>The deterministic overlap check of line spec §9.3: an observed line may not share a run of 8 or more words with the scenario's text.</summary>
internal static class HoldoutOverlap
{
    /// <summary>How many words in a row make a shared run.</summary>
    internal const int RunWords = 8;

    /// <summary>Whether the observed line repeats a run of words from any of the texts. Case, punctuation and spacing do not matter.</summary>
    /// <param name="observed">The evaluator's observed line.</param>
    /// <param name="texts">The scenario's text fields (each is read on its own, so a run never spans two).</param>
    /// <returns>True when a run of <see cref="RunWords"/> words is shared.</returns>
    internal static bool Shares(string observed, IEnumerable<string> texts)
    {
        var seen = Runs(Words(observed)).ToHashSet(StringComparer.Ordinal);
        return seen.Count > 0 && texts.Any(text => Runs(Words(text)).Any(seen.Contains));
    }

    private static string[] Words(string text) =>
    [.. text.Split([.. text.Where(static c => !char.IsLetterOrDigit(c)).Distinct()], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(static w => w.ToUpperInvariant())];

    private static IEnumerable<string> Runs(string[] words)
    {
        for (var start = 0; start + RunWords <= words.Length; start++)
        {
            yield return string.Join(' ', words.Skip(start).Take(RunWords));
        }
    }
}
