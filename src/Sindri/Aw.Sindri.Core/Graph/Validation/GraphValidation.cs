namespace Aw.Sindri.Core.Graph.Validation;

/// <summary>What the validator found.</summary>
/// <param name="Mode">The rules it applied.</param>
/// <param name="Findings">Every finding, errors and warnings, in a stable order.</param>
public sealed record GraphValidation(GraphMode Mode, IReadOnlyList<GraphFinding> Findings)
{
    /// <summary>The findings that refuse in this mode: every error on activation, every error that is not a wiring gap on a draft.</summary>
    public IReadOnlyList<GraphFinding> Blocking =>
        [.. Findings.Where(f => f.Severity == GraphSeverity.Error && (Mode == GraphMode.Activate || !f.WiringGap))];

    /// <summary>True when nothing refuses.</summary>
    public bool Passed => Blocking.Count == 0;
}
