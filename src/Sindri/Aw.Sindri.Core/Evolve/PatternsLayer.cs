using System.Text.RegularExpressions;

namespace Aw.Sindri.Core.Evolve;

/// <summary>privacy.denyPatterns, matched case-insensitively on the visible form. A pattern that doesn't compile, or that runs too long, holds: fail closed.</summary>
internal static class PatternsLayer
{
    private static readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(500);

    /// <summary>Whether any pattern matches the text.</summary>
    /// <param name="text">The candidate text.</param>
    /// <param name="patterns">The patterns.</param>
    /// <returns>True on a hit, a bad pattern or a timeout.</returns>
    public static bool Hits(string text, IReadOnlyList<string> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        var visible = PrivacyText.Visible(text);
        foreach (var pattern in patterns)
        {
            try
            {
                if (Regex.IsMatch(visible, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, _timeout))
                {
                    return true;
                }
            }
            catch (ArgumentException)
            {
                return true;   // why: a pattern that doesn't compile can't prove the text is clean
            }
            catch (RegexMatchTimeoutException)
            {
                return true;
            }
        }

        return false;
    }
}
