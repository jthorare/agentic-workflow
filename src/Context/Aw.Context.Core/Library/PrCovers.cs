using System.Text.RegularExpressions;

namespace Aw.Context.Core.Library;

/// <summary>The design-doc claims a PR body makes with its <c language="text">Covers:</c> line (spec 2026-11-04 §4.4).</summary>
/// <param name="Refs">The refs as written (backticks dropped), each once, in order; empty for <c language="text">Covers: none (...)</c>, for no line and for a doubled line.</param>
/// <param name="Declined">True for <c language="text">Covers: none (...)</c>: the PR claims no design doc.</param>
/// <param name="Doubled">True when the body has a second <c language="text">Covers:</c> line (SND-LIB-021): nothing is claimed.</param>
public sealed partial record PrCovers(IReadOnlyList<string> Refs, bool Declined, bool Doubled)
{
    /// <summary>A body with no Covers line.</summary>
    public static PrCovers None { get; } = new([], Declined: false, Doubled: false);

    /// <summary>Reads the claims out of a PR body.</summary>
    /// <param name="body">The PR body (cleaned text).</param>
    /// <returns>The claims; never null.</returns>
    public static PrCovers Parse(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return None;
        }

        var lines = new List<string>();
        var fenced = false;
        foreach (var line in body.ReplaceLineEndings("\n").Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                fenced = !fenced;
            }
            else if (!fenced && CoversLine.Match(line) is { Success: true } hit)
            {
                lines.Add(hit.Groups["rest"].Value.Trim());
            }
        }

        return lines.Count switch
        {
            0 => None,
            1 => One(lines[0]),
            _ => new PrCovers([], Declined: false, Doubled: true),
        };
    }

    private static PrCovers One(string rest)
    {
        if (NoneWord.IsMatch(rest))
        {
            return new PrCovers([], Declined: true, Doubled: false);
        }

        // why: a token that is not a ref (words after the last ref, a stray sentence) is dropped, not guessed at; reading the claim as written is the validate verb's job
        var refs = rest.Split(',')
            .Select(static t => t.Trim().Trim('`').Trim())
            .Where(static t => RefShape.IsMatch(t))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return new PrCovers(refs, Declined: false, Doubled: false);
    }

    [GeneratedRegex(@"^\s*covers\s*:(?<rest>.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CoversLine { get; }

    [GeneratedRegex(@"^none(\s|\(|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NoneWord { get; }

    [GeneratedRegex(@"^[^\s#`,]+#[^\s#`,]+$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RefShape { get; }
}
