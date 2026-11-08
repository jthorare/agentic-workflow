namespace Aw.Sindri.Core.Graph.Validation;

/// <summary>Builds the validator's findings.</summary>
internal static class Found
{
    /// <summary>An error about the whole graph or a node, with no field.</summary>
    public static GraphFinding Error(string code, IReadOnlyList<string> nodes, string message, string fix) =>
        new(code, GraphSeverity.Error, nodes, Field: null, message, fix);

    /// <summary>An error about one field of a node's config.</summary>
    public static GraphFinding At(string code, string node, string field, string message, string fix) =>
        new(code, GraphSeverity.Error, [node], field, message, fix);

    /// <summary>A wiring gap: an error a draft may keep.</summary>
    public static GraphFinding Gap(string code, IReadOnlyList<string> nodes, string message, string fix) =>
        new(code, GraphSeverity.Error, nodes, Field: null, message, fix, WiringGap: true);
}
