using System.Text.RegularExpressions;

namespace Aw.Judge.Core.PromptSort;

/// <summary>
/// The keyword fallback for every axis, and the baseline Jev's answers are blended over. Deterministic: the same prompt
/// gives the same answer. The prompt is cut to <see cref="MaxChars"/> first, so no pattern runs over a pasted file.
/// </summary>
internal static partial class PromptHeuristics
{
    internal const int MaxChars = 16_000;
    private const int RegexTimeoutMs = 1000;
    private const int ConfirmationMaxWords = 6;

    private static readonly HashSet<string> _questionStarters = new(StringComparer.Ordinal)
    {
        "what", "why", "how", "when", "where", "who", "which", "can", "could", "should", "is", "are", "do", "does", "did", "will", "would",
    };

    private static readonly string[] _confirmations =
    [
        "yes", "ok", "okay", "sure", "sounds good", "go ahead", "proceed", "continue", "looks good", "lgtm",
        "do it", "correct", "confirmed", "approved", "agreed", "no", "nope", "cancel", "stop",
    ];

    private static readonly string[] _taskVerbs =
    [
        "add", "create", "build", "implement", "refactor", "fix", "update", "change", "improve", "enhance",
        "redesign", "migrate", "integrate", "optimize", "rewrite", "replace", "remove", "delete", "write",
        "design", "develop", "extend", "modify", "generate", "configure", "set up", "clean up",
    ];

    private static readonly string[] _vagueScope =
    [
        "the app", "the system", "the api", "the codebase", "the platform", "the backend", "the frontend", "the ui",
        "the project", "the entire", "everything", "all of it", "the whole thing",
        "endpoints", "components", "tests", "files", "bugs", "issues", "features",
    ];

    private static readonly string[] _limiters = ["only", "just", "specifically", "excluding", "except", "up to", "no more than", "limit to", "solely"];

    private static readonly string[] _acceptance =
        ["acceptance criteria", "when done", "success looks like", "verify with", "verify that", "done when", "definition of done"];

    // "with" matches nearly every sentence, so it is left out.
    private static readonly string[] _approach = ["using", "via", "through", "by using", "based on"];

    private static readonly string[] _loopTriggers =
    [
        "run until done", "do all", "do it all", "keep going", "iterate until", "finish this", "complete everything",
        "don't stop", "dont stop", "until complete", "until finished", "until done", "loop mode", "autonomous mode",
    ];

    /// <summary>Heuristic values for the eleven axes.</summary>
    /// <param name="prompt">The prompt as typed.</param>
    /// <returns>The values.</returns>
    internal static SortValues Sort(string prompt)
    {
        var raw = prompt.Length > MaxChars ? prompt[..MaxChars] : prompt;
        var lower = raw.ToLowerInvariant().Trim();
        var text = new PhraseText(lower);
        var task = IsTaskShaped(lower, text);
        var acceptance = text.HasAny(_acceptance);
        return new SortValues(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SortAxes.IsTask] = Flag(task),
            [SortAxes.WantsLoop] = Flag(text.HasAny(_loopTriggers)),
            [SortAxes.ScopeDefined] = Flag(PathLike.IsMatch(raw) || FileName.IsMatch(raw) || Backticked.IsMatch(raw)),
            [SortAxes.LimitsDefined] = Flag(text.HasAny(_limiters) || acceptance),
            [SortAxes.ApproachDefined] = Flag(text.HasAny(_approach)),
            [SortAxes.VerificationDefined] = Flag(acceptance || Verification.IsMatch(lower)),
            [SortAxes.IsBugReport] = Flag(Bug.IsMatch(lower)),
            [SortAxes.TouchesUi] = Flag(Ui.IsMatch(lower) || UiFile.IsMatch(raw)),
            [SortAxes.NeedsResearch] = Flag(Research.IsMatch(lower)),
            [SortAxes.Complexity] = ComplexityLevel(text, task),
            [SortAxes.Ambiguity] = AmbiguityLevel(raw, text, task),
        });
    }

    /// <summary>Whether a short reply only confirms or declines ('yes', 'go ahead', 'lgtm').</summary>
    /// <param name="text">The reply.</param>
    /// <returns>True when it is one.</returns>
    internal static bool IsConfirmation(string text)
    {
        var words = Words(text.ToLowerInvariant());
        if (words.Count is 0 or > ConfirmationMaxWords)
        {
            return false;
        }

        var normalized = string.Join(' ', words);
        return _confirmations.Any(c =>
        {
            var bare = string.Join(' ', Words(c));
            return string.Equals(normalized, bare, StringComparison.Ordinal) || normalized.StartsWith(bare + " ", StringComparison.Ordinal);
        });
    }

    // Runs of a-z and apostrophes in lower-cased text.
    internal static List<string> Words(string lower)
    {
        var words = new List<string>();
        var start = -1;
        for (var i = 0; i <= lower.Length; i++)
        {
            var inWord = i < lower.Length && (lower[i] is (>= 'a' and <= 'z') or '\'');
            if (inWord && start < 0)
            {
                start = i;
            }
            else if (!inWord && start >= 0)
            {
                words.Add(lower[start..i]);
                start = -1;
            }
        }

        return words;
    }

    private static string Flag(bool value) => value ? SortAxes.Yes : SortAxes.No;

    private static bool IsTaskShaped(string lower, PhraseText text)
    {
        if (lower.Length == 0 || IsQuestion(lower) || IsConfirmation(lower))
        {
            return false;
        }

        return text.HasAny(_taskVerbs);
    }

    private static bool IsQuestion(string lower) =>
        lower.TrimEnd().EndsWith('?') || (Words(lower) is [var first, ..] && _questionStarters.Contains(first));

    [GeneratedRegex(@"[\w.-]/[\w./-]", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, RegexTimeoutMs)]
    private static partial Regex PathLike { get; }

    [GeneratedRegex(@"[\w-]\.(?:py|ts|tsx|js|jsx|md|json|yaml|yml|go|rb|java|rs|css|html|swift)\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase, RegexTimeoutMs)]
    private static partial Regex FileName { get; }

    [GeneratedRegex(@"`[^`]+`", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, RegexTimeoutMs)]
    private static partial Regex Backticked { get; }

    [GeneratedRegex(@"\b(tests?|typecheck|passes|passing|verify|verified|lint|screenshot|acceptance|expected)\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, RegexTimeoutMs)]
    private static partial Regex Verification { get; }

    [GeneratedRegex(@"\b(bugs?|broken|crash(?:es|ed|ing)?|regression|exception|stack trace|traceback|fails|failing|failed|doesn'?t work|not working|isn'?t working|throws|wrong|incorrect)\b|\berror\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, RegexTimeoutMs)]
    private static partial Regex Bug { get; }

    [GeneratedRegex(@"\b(ui|ux|screen|page|button|modal|dialog|layout|css|style|styling|styles|components?|swiftui|tab|dropdown|form|responsive|dark mode|spacing|icon|navbar|sidebar|tooltip|toast|animation)\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, RegexTimeoutMs)]
    private static partial Regex Ui { get; }

    [GeneratedRegex(@"\.(?:tsx|jsx|css|scss|swift)\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase, RegexTimeoutMs)]
    private static partial Regex UiFile { get; }

    [GeneratedRegex(@"\b(investigate|research|find out|look into|figure out|explore|compare|root cause|why (?:does|is|are|do))\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, RegexTimeoutMs)]
    private static partial Regex Research { get; }

    [GeneratedRegex(@"\b\d+(?:\.\d+)?\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, RegexTimeoutMs)]
    private static partial Regex Number { get; }
}
