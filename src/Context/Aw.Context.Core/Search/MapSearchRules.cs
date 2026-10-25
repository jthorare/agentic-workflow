using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Context.Core.Search;

/// <summary>The limits of a map search and how a question is cut into words.</summary>
public static partial class MapSearchRules
{
    /// <summary>The hits returned when the caller names no limit.</summary>
    public const int DefaultLimit = 50;

    /// <summary>The most hits one answer holds; 'total' says how many matched in all.</summary>
    public const int MaxLimit = 200;

    /// <summary>The most words one question uses.</summary>
    public const int MaxTerms = 12;

    private const int MaxQuestion = 512;
    private const int MaxTermLength = 64;

    /// <summary>The words of a question: runs of letters and digits, lower-cased, at most <see cref="MaxTerms"/> of them, each cut to 64 characters.</summary>
    /// <param name="q">The question as typed.</param>
    /// <returns>The words, or SND-CONTEXT-006 for a question with none or one over 512 characters.</returns>
    public static Result<IReadOnlyList<string>> Terms(string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return ContextErrors.QueryInvalid.With("the search needs words to find (fix: pass q, for example q=tab manager)");
        }

        if (q.Length > MaxQuestion)
        {
            return ContextErrors.QueryInvalid.With($"the search question is {q.Length} characters; the limit is {MaxQuestion} (fix: ask with fewer words)");
        }

        string[] terms = [.. Word.Matches(q).Select(static m => m.Value.ToLowerInvariant()).Select(static w => w.Length > MaxTermLength ? w[..MaxTermLength] : w).Take(MaxTerms)];
        if (terms.Length == 0)
        {
            return ContextErrors.QueryInvalid.With("the search question holds no letters or digits (fix: ask with words, for example q=tab manager)");
        }

        return terms;
    }

    /// <summary>The limit asked, checked.</summary>
    /// <param name="limit">The limit, or null for the default.</param>
    /// <returns>The limit to use, or SND-CONTEXT-006 outside 1 to <see cref="MaxLimit"/>.</returns>
    public static Result<int> CheckLimit(int? limit)
    {
        if (limit is null)
        {
            return DefaultLimit;
        }

        return limit is >= 1 and <= MaxLimit
            ? limit.Value
            : ContextErrors.QueryInvalid.With($"limit is {limit.Value.ToString(CultureInfo.InvariantCulture)}; it is 1 to {MaxLimit} (fix: ask for fewer hits; 'total' says how many matched)");
    }

    [GeneratedRegex("[\\p{L}\\p{N}]+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Word { get; }
}
