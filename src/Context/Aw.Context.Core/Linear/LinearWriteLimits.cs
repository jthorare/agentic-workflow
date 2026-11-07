using System.Globalization;
using Aw.Kernel;

namespace Aw.Context.Core.Linear;

/// <summary>The checks of a Linear write that Context makes before anything is sent (Linear hub spec 2026-11-04, input limits, §9): text limits, an empty change, an attachment's url and title. Each refusal is SND-LINEAR-015 reading 'Not sent: (the reason). Nothing was changed.'; 'Linear said no: ...' stays for Linear's own refusal. Lengths count UTF-16 characters.</summary>
public static class LinearWriteLimits
{
    /// <summary>The longest title.</summary>
    public const int TitleMax = 255;

    /// <summary>The longest description or comment.</summary>
    public const int TextMax = 65_536;

    /// <summary>Checks an issue title.</summary>
    /// <param name="title">The title.</param>
    /// <returns>SND-LINEAR-015 when it is empty, only whitespace or over <see cref="TitleMax"/>, else null.</returns>
    public static Error? Title(string? title) =>
        title is { Length: > 0 and <= TitleMax } && !string.IsNullOrWhiteSpace(title) ? null : Refused(string.Create(CultureInfo.InvariantCulture, $"a title is 1 to {TitleMax} characters (this one has {title?.Length ?? 0})"));

    /// <summary>Checks a description, or a comment's text.</summary>
    /// <param name="what">'description' or 'comment'.</param>
    /// <param name="text">The text; null passes unless <paramref name="required"/>.</param>
    /// <param name="required">True for a comment: it can't be empty. A description may be empty or null (it clears the field).</param>
    /// <returns>SND-LINEAR-015 when it is over <see cref="TextMax"/> (or empty and required), else null.</returns>
    public static Error? Text(string what, string? text, bool required) =>
        text is { Length: > TextMax } || (required && string.IsNullOrEmpty(text))
            ? Refused(string.Create(CultureInfo.InvariantCulture, $"a {what} is {(required ? "1" : "0")} to {TextMax:N0} characters (this one has {text?.Length ?? 0:N0})"))
            : null;

    /// <summary>Checks an issue change before it is sent.</summary>
    /// <param name="change">The change.</param>
    /// <returns>SND-LINEAR-015 when it sets no field, or its title or description breaks a limit, else null.</returns>
    public static Error? Change(LinearIssueChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return change.IsEmpty
            ? Refused("the change sets no field")
            : (change.Title.IsSet ? Title(change.Title.Value) : null) ?? (change.Description.IsSet ? Text("description", change.Description.Value, required: false) : null);
    }

    /// <summary>Checks an attachment before it is sent (spec §9, 'Attachment urls').</summary>
    /// <param name="url">The link: absolute, https (a pull request) or 'sindri://item/...'.</param>
    /// <param name="title">The attachment's title, 1 to 255 characters.</param>
    /// <returns>SND-LINEAR-015 when the url or the title is refused, else null.</returns>
    public static Error? Attachment(Uri url, string? title)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!url.IsAbsoluteUri)
        {
            return Refused("an attachment's link must be an absolute url");
        }

        var allowed = url.Scheme == Uri.UriSchemeHttps || (string.Equals(url.Scheme, "sindri", StringComparison.Ordinal) && string.Equals(url.Host, "item", StringComparison.Ordinal));
        return allowed ? Title(title) : Refused("an attachment's link is an https url or sindri://item/...");
    }

    private static Error Refused(string detail) => LinearHubErrors.Rejected.With($"Not sent: {detail}. Nothing was changed.");
}
