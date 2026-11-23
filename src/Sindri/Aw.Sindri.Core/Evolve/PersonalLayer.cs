using System.Text.RegularExpressions;

namespace Aw.Sindri.Core.Evolve;

/// <summary>An email address or a home path ('/home/x', '/Users/x', 'C:\Users\x') in the visible form.</summary>
internal static partial class PersonalLayer
{
    /// <summary>Whether the text holds an email address or a home path.</summary>
    /// <param name="text">The candidate text.</param>
    /// <returns>True on a hit.</returns>
    public static bool Hits(string text)
    {
        var visible = PrivacyText.Visible(text);
        try
        {
            return Email.IsMatch(visible) || HomePath.IsMatch(visible);
        }
        catch (RegexMatchTimeoutException)
        {
            return true;   // why: text that can't be read in time can't be shown clean
        }
    }

    [GeneratedRegex(@"[\p{L}\p{N}._%+-]+@[\p{L}\p{N}-]+(?:\.[\p{L}\p{N}-]+)*\.\p{L}{2,}", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Email { get; }

    [GeneratedRegex(@"(?:/home/|/Users/|[A-Za-z]:\\Users\\)[^\s/\\]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HomePath { get; }
}
