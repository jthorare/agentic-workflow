using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Tests.Workflow.Wiring;

/// <summary>What a rule-group run is located in and checked against: the file, the node positions the reader would record, and the context.</summary>
/// <param name="File">The file name findings show.</param>
/// <param name="Positions">Each recorded node's path and position.</param>
/// <param name="Catalog">The catalog; the test catalog when null.</param>
/// <param name="Profile">The profile facts; none when null.</param>
/// <param name="Children">The child resolver; none when null.</param>
internal sealed record Located(
    string File,
    IReadOnlyList<(string Path, int Line, int Column)> Positions,
    BlockCatalog? Catalog = null,
    RecipeProfileFacts? Profile = null,
    IChildRecipeResolver? Children = null)
{
    /// <summary>No file positions: every finding is at r.yaml:1:1.</summary>
    public static Located Nowhere { get; } = new("r.yaml", []);
}
