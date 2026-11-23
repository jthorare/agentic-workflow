namespace Aw.Sindri.Core.Graph.Templates;

internal static partial class StyleRuleProposer
{
    /// <summary>'check', once at the start: red stub.</summary>
    /// <param name="top">How many findings to hand on.</param>
    /// <returns>The code body.</returns>
    public static string CheckCode(int top) => Lines([$"throw new NotSupportedException(\"red: check hands on the first {top} findings\");"]);

    /// <summary>'vet', once per finding: red stub.</summary>
    public static string VetCode { get; } = Lines(["throw new NotSupportedException(\"red: vet\");"]);

    /// <summary>'entry', once per draft: red stub.</summary>
    public static string EntryCode { get; } = Lines(["throw new NotSupportedException(\"red: entry\");"]);

    /// <summary>'summary', once at the end: red stub.</summary>
    public static string SummaryCode { get; } = Lines(["throw new NotSupportedException(\"red: summary\");"]);

    // why: the bodies are kept as lines joined with "\n", never a multi-line literal (MA0136), so the text the runner compiles is the same on every OS
    private static string Lines(string[] lines) => string.Join('\n', lines);
}
