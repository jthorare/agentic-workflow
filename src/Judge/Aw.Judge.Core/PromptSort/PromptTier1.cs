using System.Text.RegularExpressions;

namespace Aw.Judge.Core.PromptSort;

/// <summary>Prompts the sorter must not spend a Jev request on and must never scaffold: machine text, slash commands, confirmations.</summary>
public static partial class PromptTier1
{
    private const int MinChars = 4;
    private const int RegexTimeoutMs = 1000;

    // The machine prefixes mirror turn-origin.sh.
    private static readonly string[] _machinePrefixes =
        ["<", "[Request interrupted by user", "Another Claude session sent a message", "Base directory for this skill", "Caveat:", "This session is being continued"];

    /// <summary>Why a prompt is skipped, or null when it is sorted.</summary>
    /// <param name="prompt">The prompt as typed.</param>
    /// <returns>'empty', 'machine', 'slash-command', 'too-short' or 'confirmation'; null for a real prompt.</returns>
    public static string? SkipReason(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        var text = prompt.Trim();
        if (text.Length == 0)
        {
            return "empty";
        }

        if (_machinePrefixes.Any(prefix => text.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return "machine";
        }

        // "/bugFixOrchestrator FRN-1", "/clear": a slash word then space or end. A pasted absolute path has '/' after its first segment.
        if (SlashCommand.IsMatch(text))
        {
            return "slash-command";
        }

        if (text.Length < MinChars)
        {
            return "too-short";
        }

        return PromptHeuristics.IsConfirmation(text) ? "confirmation" : null;
    }

    [GeneratedRegex(@"^/[A-Za-z][\w:-]*(\s|$)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, RegexTimeoutMs)]
    private static partial Regex SlashCommand { get; }
}
