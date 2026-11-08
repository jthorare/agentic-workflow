namespace Aw.Sindri.Core.Graph.Validation;

/// <summary>One thing wrong with a graph: where it is and how to fix it.</summary>
/// <param name="Code">A stable code such as 'GRAPH-TRIGGER-001'.</param>
/// <param name="Severity">Error or warning.</param>
/// <param name="Nodes">The nodes it names, in spec order; empty for a finding about the whole graph.</param>
/// <param name="Field">The config field it names, such as 'config.prompt', or null.</param>
/// <param name="Message">What is wrong.</param>
/// <param name="Fix">What to change.</param>
/// <param name="WiringGap">True for a gap a draft may keep (no trigger yet, a node not yet connected); activation refuses it.</param>
public sealed record GraphFinding(string Code, GraphSeverity Severity, IReadOnlyList<string> Nodes, string? Field, string Message, string Fix, bool WiringGap = false)
{
    /// <summary>The finding as one line: code, where, what is wrong, then the fix.</summary>
    /// <returns>The line.</returns>
    public override string ToString()
    {
        var where = Nodes.Count == 0 ? string.Empty : $" node '{string.Join("', '", Nodes)}'{(Field is null ? string.Empty : $" field '{Field}'")}";
        return $"{Code}{where}: {Message}; {Fix}";
    }
}
