using System.Globalization;
using System.Text.RegularExpressions;

namespace Aw.Redaction;

/// <summary>
/// Checks configured extra patterns against the rules the built-in ones follow (docs/csharp/redaction.md "Pattern
/// rules"): an id that can sit in a placeholder and names no built-in rule, and a pattern that parses with the options
/// every rule gets and uses explicit ASCII classes instead of the Unicode-aware shorthands.
/// </summary>
internal static partial class ExtraPatternRules
{
    /// <summary>The longest id accepted.</summary>
    public const int MaxIdLength = 64;

    /// <summary>The longest pattern accepted.</summary>
    public const int MaxPatternLength = 1024;

    // Placeholder names that are not rules: a later scrub skips them, so an extra pattern may not take them.
    private static readonly string[] _reserved = ["blocked", "secret-field"];

    /// <summary>Every refused field of <paramref name="extras"/>, in entry order (an entry's id before its pattern).</summary>
    /// <param name="extras">The configured patterns.</param>
    /// <param name="builtinIds">The built-in rule ids, which an extra pattern may not reuse.</param>
    /// <param name="matchTimeout">The redactor's match timeout, for trying a pattern on an empty text.</param>
    /// <returns>The errors; empty when every entry is accepted.</returns>
    public static List<ExtraPatternError> Check(IReadOnlyList<ExtraPattern> extras, IReadOnlyList<string> builtinIds, TimeSpan matchTimeout)
    {
        var errors = new List<ExtraPatternError>();
        var taken = new HashSet<string>(builtinIds.Concat(_reserved), StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < extras.Count; index++)
        {
            if (extras[index] is not var (id, pattern))
            {
                // A null entry comes from configuration (an empty list item), so it is refused, not a bug.
                errors.Add(new ExtraPatternError(index, ExtraPatternField.Id, RedactionCodes.InvalidPatternId, "The entry is empty; give it an id and a pattern."));
                continue;
            }

            if (IdProblem(id, taken, seen) is { } idProblem)
            {
                errors.Add(new ExtraPatternError(index, ExtraPatternField.Id, RedactionCodes.InvalidPatternId, idProblem));
            }

            if (PatternProblem(pattern, matchTimeout) is { } patternProblem)
            {
                errors.Add(new ExtraPatternError(index, ExtraPatternField.Pattern, RedactionCodes.InvalidPattern, patternProblem));
            }
        }

        return errors;
    }

    private static string? IdProblem(string? id, HashSet<string> taken, HashSet<string> seen)
    {
        if (id is null || id.Length > MaxIdLength || !IdShape.IsMatch(id))
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"The id must be lowercase ASCII letters, digits and single hyphens, start with a letter, and be at most {MaxIdLength} characters (e.g. acme-customer-id); it is the name in [REDACTED:<id>].");
        }

        if (taken.Contains(id))
        {
            return $"The id '{id}' is taken by a built-in redaction rule or placeholder; choose another name.";
        }

        return seen.Add(id) ? null : $"The id '{id}' is already used by an earlier extra pattern; give each pattern its own id.";
    }

    private static string? PatternProblem(string? pattern, TimeSpan matchTimeout)
    {
        if (string.IsNullOrEmpty(pattern) || pattern.Length > MaxPatternLength)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"The pattern must be a non-empty .NET regular expression of at most {MaxPatternLength} characters.");
        }

        try
        {
            // Parsed and tried on an empty text here; the pattern is compiled per size tier, with the redactor's timeout,
            // when a scrub needs it.
            if (new Regex(pattern, CompiledRules.Always, matchTimeout).IsMatch(string.Empty))
            {
                return "The pattern can match an empty text, so it would match at every position; make at least one part required.";
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // Refused, never thrown, so every refusal comes back as an error. No pattern found in review times out on an
            // empty text (the engine cuts empty loop iterations), so this is a guard for one that would block every scrub.
            return "The pattern backtracks so much that it timed out on an empty text; remove nested or overlapping repetition.";
        }
        catch (RegexParseException parse)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"The pattern is not a valid .NET regular expression ({parse.Error} at offset {parse.Offset}).");
        }

        return ShorthandProblem(pattern);
    }

    // \d, \w, \s and \b (and their negations) are Unicode-aware in .NET, and \p{...} names a Unicode category, so they
    // match far more than the ASCII shapes a pattern means (Arabic-Indic digits, letters of every script); the built-in
    // rules spell the ASCII classes out. The scan is lexical: such an escape inside a class ([\b] is a backspace there) or
    // an (?x) comment is refused too, the safe direction.
    private static string? ShorthandProblem(string pattern)
    {
        for (var at = 0; at < pattern.Length - 1; at++)
        {
            if (pattern[at] != '\\')
            {
                continue;
            }

            var replacement = char.ToLowerInvariant(pattern[at + 1]) switch
            {
                'd' => "[0-9]",
                'w' => "[A-Za-z0-9_]",
                's' => "[ \\t\\r\\n]",
                'b' => "a lookaround such as (?<![A-Za-z0-9_])",
                'p' => "the ASCII range you mean, such as [0-9] or [A-Za-z]",
                _ => null,
            };
            if (replacement is not null)
            {
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"The pattern uses \\{pattern[at + 1]} at offset {at}, which is Unicode-aware in .NET; write an explicit ASCII class instead: {replacement} (or its negation).");
            }

            at++; // Skip the escaped character, so an escaped backslash ("\\d") is a backslash and a 'd'.
        }

        return null;
    }

    // \z, not $: in .NET, $ also matches before a final newline.
    [GeneratedRegex("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*\\z", CompiledRules.Always, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IdShape { get; }
}
