namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>A recipe file's name and its node positions: what every finding about it is located against.</summary>
/// <param name="File">The file's name as findings show it: the file name without its directory ('RCP-REACH-001.yaml'), or the name a text request gave.</param>
/// <param name="Positions">Where each node starts.</param>
public sealed record RecipeSource(string File, SourcePositions Positions)
{
    /// <summary>A finding about the node at <paramref name="path"/>, located in this file.</summary>
    /// <param name="rule">The rule id.</param>
    /// <param name="path">The node's path.</param>
    /// <param name="message">What is wrong.</param>
    /// <param name="fix">What to do about it.</param>
    /// <param name="severity">Error unless the rule says otherwise.</param>
    /// <returns>The finding.</returns>
    public RecipeFinding Finding(string rule, string path, string message, string fix, FindingSeverity severity = FindingSeverity.Error)
    {
        var at = Positions.Locate(path);
        return new RecipeFinding(rule, message, File, at.Line, at.Column, path, fix, severity);
    }
}
