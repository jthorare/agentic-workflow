using System.Globalization;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// One validator or reader finding (spec §9.1). <see cref="ToLine"/> prints the line every surface shows:
/// 'RCP-…: &lt;what&gt; at &lt;file&gt;:&lt;line&gt;:&lt;col&gt; (&lt;path&gt;); &lt;fix&gt;'.
/// </summary>
/// <param name="Rule">The rule id, for example 'RCP-REACH-001'.</param>
/// <param name="Message">What is wrong, for example "state 'orphan' is unreachable from 'produce'".</param>
/// <param name="File">The recipe file's name (<see cref="RecipeSource.File"/>).</param>
/// <param name="Line">The node's line, from 1.</param>
/// <param name="Column">The node's column, from 1.</param>
/// <param name="Path">The node's path, for example 'states.orphan' (<see cref="SourcePositions"/>).</param>
/// <param name="Fix">What to do about it.</param>
/// <param name="Severity">Error, information or skipped.</param>
public sealed record RecipeFinding(
    string Rule,
    string Message,
    string File,
    int Line,
    int Column,
    string Path,
    string Fix,
    FindingSeverity Severity)
{
    /// <summary>The finding as one line.</summary>
    /// <returns>'RCP-…: &lt;what&gt; at &lt;file&gt;:&lt;line&gt;:&lt;col&gt; (&lt;path&gt;); &lt;fix&gt;'.</returns>
    public string ToLine() => string.Create(CultureInfo.InvariantCulture, $"{Rule}: {Message} at {File}:{Line}:{Column} ({Path}); {Fix}");
}
