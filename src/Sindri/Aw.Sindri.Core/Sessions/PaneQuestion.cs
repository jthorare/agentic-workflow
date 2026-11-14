namespace Aw.Sindri.Core.Sessions;

/// <summary>The pending question of an asking session, read from its pane's tail (Agents page spec §7, issue 3082). Pure.</summary>
public static class PaneQuestion
{
    /// <summary>The most lines a question keeps.</summary>
    public const int MaxLines = 3;

    /// <summary>The last <see cref="MaxLines"/> non-empty lines, joined with a line feed; null when there are none.</summary>
    /// <param name="lines">The pane's lines, already stripped of escapes and control characters.</param>
    /// <returns>The question text, or null.</returns>
    public static string? Of(IReadOnlyList<string> lines) => throw new NotSupportedException();
}
