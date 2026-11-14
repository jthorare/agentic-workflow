using Aw.Sindri.Core.Graph.Validation;

namespace Aw.Sindri.Features.ValidateWorkflow;

/// <summary>One finding of the validator, as the API shows it.</summary>
/// <param name="Code">The finding's code, such as GRAPH-REF-001.</param>
/// <param name="Severity">'error' or 'warning'.</param>
/// <param name="Nodes">The nodes it names.</param>
/// <param name="Field">The config field it names, or null.</param>
/// <param name="Message">What is wrong.</param>
/// <param name="Fix">How to fix it.</param>
/// <param name="Id">A stable id for the finding, '&lt;code&gt;:&lt;node&gt;:&lt;field&gt;' with '-' for a missing part, so the page can keep its place across saves.</param>
/// <param name="Node">The node the finding belongs to (the first of <paramref name="Nodes"/>), or null for a finding about the whole graph.</param>
/// <param name="Cause">What is wrong, in one line (the same text as <paramref name="Message"/>).</param>
internal sealed record WorkflowFinding(string Code, string Severity, IReadOnlyList<string> Nodes, string? Field, string Message, string Fix, string Id, string? Node, string Cause)
{
    /// <summary>Builds the finding from the validator's, with its stable id, node and cause filled in.</summary>
    /// <param name="finding">The validator's finding.</param>
    /// <returns>The finding.</returns>
    internal static WorkflowFinding Of(GraphFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        var node = finding.Nodes.Count > 0 ? finding.Nodes[0] : null;
        return new WorkflowFinding(finding.Code, finding.Severity == GraphSeverity.Error ? "error" : "warning", finding.Nodes, finding.Field, finding.Message, finding.Fix,
            $"{finding.Code}:{node ?? "-"}:{finding.Field ?? "-"}", node, finding.Message);
    }
}
