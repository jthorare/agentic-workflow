using System.Globalization;
using Aw.Kernel;

namespace Aw.Context.Core.Linear;

/// <summary>The text limits of a Linear write (Linear hub spec 2026-11-04, input limits), checked before anything is sent so the caller sees the same answer as when Linear refuses: SND-LINEAR-015, nothing changed. Lengths count UTF-16 characters.</summary>
public static class LinearWriteLimits
{
    /// <summary>The longest title.</summary>
    public const int TitleMax = 255;

    /// <summary>The longest description or comment.</summary>
    public const int TextMax = 65_536;

    /// <summary>Checks an issue title.</summary>
    /// <param name="title">The title.</param>
    /// <returns>SND-LINEAR-015 when it is empty or over <see cref="TitleMax"/>, else null.</returns>
    public static Error? Title(string? title) =>
        title is { Length: > 0 and <= TitleMax } ? null : Refused(string.Create(CultureInfo.InvariantCulture, $"a title is 1 to {TitleMax} characters (this one has {title?.Length ?? 0})"));

    /// <summary>Checks a description, or a comment's text.</summary>
    /// <param name="what">'description' or 'comment'.</param>
    /// <param name="text">The text; null passes unless <paramref name="required"/>.</param>
    /// <param name="required">True for a comment: it can't be empty. A description may be empty or null (it clears the field).</param>
    /// <returns>SND-LINEAR-015 when it is over <see cref="TextMax"/> (or empty and required), else null.</returns>
    public static Error? Text(string what, string? text, bool required) =>
        text is { Length: > TextMax } || (required && string.IsNullOrEmpty(text))
            ? Refused(string.Create(CultureInfo.InvariantCulture, $"a {what} is {(required ? "1" : "0")} to {TextMax:N0} characters (this one has {text?.Length ?? 0:N0})"))
            : null;

    private static Error Refused(string detail) => LinearHubErrors.Rejected.With($"Linear said no: {detail}. Nothing was changed.");
}
