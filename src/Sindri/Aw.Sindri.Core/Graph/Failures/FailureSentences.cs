namespace Aw.Sindri.Core.Graph.Failures;

/// <summary>The one catalog that maps a failure code to its sentence and fix; the UI, the CLI and MCP print the same words (spec §6.1, E9).</summary>
public static class FailureSentences
{
    /// <summary>Finds the row for a code.</summary>
    /// <param name="code">The failure code.</param>
    /// <returns>The row, or null when the code has none.</returns>
    public static FailureSentence? Find(string code) => throw new NotSupportedException("P3e: the failure catalog is not built yet");

    /// <summary>Fills a row's sentence from the finding's step title and config values (never a node id).</summary>
    /// <param name="code">The failure code.</param>
    /// <param name="values">Placeholder name (without braces) to value.</param>
    /// <returns>The sentence with every placeholder filled; an unfilled placeholder is an error, not left in the text.</returns>
    public static string Say(string code, IReadOnlyDictionary<string, string> values) => throw new NotSupportedException("P3e: the failure catalog is not built yet");
}
