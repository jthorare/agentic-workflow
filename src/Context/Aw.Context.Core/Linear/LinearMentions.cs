namespace Aw.Context.Core.Linear;

/// <summary>
/// Whether a comment body mentions a person (Linear hub spec 2026-11-04 §4.3). The syntax is an assumption Ph0's probe checks against the real API: an '@name' in the text, or the person's profile path ('/profiles/name') in a link, where
/// the name is their name or their display name, matched without regard to case and only as a whole word.
/// </summary>
public static class LinearMentions
{
    private const string ProfilePath = "/profiles/";

    /// <summary>Whether <paramref name="body"/> mentions anyone of these names.</summary>
    /// <param name="body">The comment's text.</param>
    /// <param name="names">The viewer's names (name, display name); blank ones are ignored.</param>
    /// <returns>True when one of them is mentioned.</returns>
    public static bool Mentions(string body, IEnumerable<string?> names)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(names);
        return names.OfType<string>().Where(static n => !string.IsNullOrWhiteSpace(n)).Select(static n => n.Trim()).Any(n => Contains(body, "@" + n) || Contains(body, ProfilePath + n));
    }

    private static bool Contains(string body, string needle)
    {
        for (var from = 0; from <= body.Length - needle.Length;)
        {
            var at = body.IndexOf(needle, from, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                return false;
            }

            var end = at + needle.Length;
            if ((end == body.Length || !IsNameChar(body[end])) && (needle[0] != '@' || at == 0 || !IsWordChar(body[at - 1])))
            {
                return true;
            }

            from = at + 1;
        }

        return false;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    // why: Linear names can carry a hyphen ('Joi-Smith'), so '@Joi' must not match the start of '@Joi-Smith'
    private static bool IsNameChar(char c) => IsWordChar(c) || c == '-';
}
