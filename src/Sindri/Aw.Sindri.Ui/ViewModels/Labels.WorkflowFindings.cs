using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the server's validation findings in the Workflows editor (#3803). The finding's sentence and fix line come from WorkflowFindingWords (#4397); the chip and the header use one term, "to fix".</summary>
internal static partial class Labels
{
    /// <summary>The expander under a finding that holds the server's own message.</summary>
    internal const string WfFindingDetails = "Details";

    /// <summary>The fix button of a finding the app can fix (GRAPH-PROMPT-001: open the step's question).</summary>
    internal const string WfFindingFix = "Fix this";

    /// <summary>The findings header over the graph: "1 thing to fix", "3 things to fix"; empty with none.</summary>
    /// <param name="count">How many findings the server sent.</param>
    /// <returns>The words.</returns>
    internal static string WfFindingsHead(int count) => count switch
    {
        <= 0 => string.Empty,
        1 => "1 thing to fix",
        _ => count.ToString(CultureInfo.InvariantCulture) + " things to fix",
    };

    /// <summary>The mark on a step's card: "⚠ 1" for warnings only, "✕ 2" when one is an error; empty with none. The glyph differs by kind, so the mark never relies on colour alone.</summary>
    /// <param name="count">How many findings name the step.</param>
    /// <param name="error">True when one of them is an error.</param>
    /// <returns>The words.</returns>
    internal static string WfFindingMark(int count, bool error) =>
        count <= 0 ? string.Empty : (error ? "✕ " : "⚠ ") + count.ToString(CultureInfo.InvariantCulture);

    /// <summary>A marked step's accessible name: its name, then the number of things to fix.</summary>
    /// <param name="name">The step's name: title, type and status word.</param>
    /// <param name="count">How many findings name the step.</param>
    /// <returns>The words.</returns>
    internal static string WfFindingNodeName(string name, int count) => name + ", " + WfFindingsHead(count);

    /// <summary>A finding's accessible name: its tag, the sentence and its fix line.</summary>
    /// <param name="sentence">What is wrong, as the server says it.</param>
    /// <param name="fix">How to fix it, as the server says it.</param>
    /// <returns>The words.</returns>
    internal static string WfFindingName(string sentence, string fix) => WfFindingWord + ": " + sentence + " " + fix;

    /// <summary>The chip word of a finding, error or warning: "To fix". The glyph and the tone tell the two apart, and the header says "things to fix".</summary>
    internal const string WfFindingWord = "To fix";
}
