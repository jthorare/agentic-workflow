using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.ListWorkflowBlocks;

/// <summary>One block type, straight from its definition.</summary>
/// <param name="Type">The type a node names, such as 'schedule'.</param>
/// <param name="Title">The label the picker shows.</param>
/// <param name="IsTrigger">True for a block that starts runs.</param>
/// <param name="Config">The config fields.</param>
/// <param name="Outputs">The output fields a later node may reference, by name and type (for a block whose outputs depend on its config, the shape of its example).</param>
/// <param name="Branches">The branch names an edge may leave by; 'out' for a block that does not branch.</param>
/// <param name="Example">An example config.</param>
internal sealed record BlockView(string Type, string Title, bool IsTrigger, IReadOnlyList<BlockFieldView> Config, IReadOnlyDictionary<string, string> Outputs, IReadOnlyList<string> Branches, JsonNode? Example);
