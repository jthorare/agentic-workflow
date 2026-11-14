namespace Aw.Sindri.Features.StartSession;

/// <summary>The title a started session shows until its agent names it: the first non-empty line of the start prompt, trimmed and cut to 80 characters (Agents page, issue 3082).</summary>
internal static class StartTitle
{
    internal const int MaxLength = 80;

    /// <summary>The title of <paramref name="prompt"/>, or null when it has no text.</summary>
    /// <param name="prompt">The start prompt.</param>
    /// <returns>The title, or null.</returns>
    internal static string? Of(string? prompt) => throw new NotSupportedException();
}
