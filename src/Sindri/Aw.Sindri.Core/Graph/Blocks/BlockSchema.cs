using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>A block's config schema: the fields it takes. Anything else in the config is a finding.</summary>
/// <param name="Fields">The fields.</param>
public sealed record BlockSchema(IReadOnlyList<BlockField> Fields)
{
    /// <summary>Checks <paramref name="config"/> against the fields.</summary>
    /// <param name="config">The node's config.</param>
    /// <returns>One problem per field: the field's name and what to change; empty when the config fits.</returns>
    public IReadOnlyList<(string Field, string Problem)> Check(JsonObject config) => throw new NotSupportedException();
}
