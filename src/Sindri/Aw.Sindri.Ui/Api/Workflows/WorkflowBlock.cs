using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>One block type of the picker.</summary>
/// <param name="Type">The type a node names, such as "schedule".</param>
/// <param name="Title">The label the picker shows.</param>
/// <param name="IsTrigger">True for a block that starts runs.</param>
/// <param name="Config">The config fields; the editor's form is made from them.</param>
/// <param name="Outputs">The output fields a later node may reference, by name and type.</param>
/// <param name="Branches">The branch names an edge may leave by.</param>
/// <param name="ExampleConfig">An example config, kept raw, or null.</param>
/// <param name="ExampleSpec">A whole example workflow using the block, kept raw, or null.</param>
/// <param name="OneOf">Groups of field names of which exactly one must be filled ("duration" and "until" of a wait); null when the block has no such rule or an older Sindri did not send it (#4359).</param>
internal sealed record WorkflowBlock(
    string Type,
    string Title,
    bool IsTrigger,
    IReadOnlyList<WorkflowBlockField> Config,
    IReadOnlyDictionary<string, string> Outputs,
    IReadOnlyList<string> Branches,
    JsonElement? ExampleConfig,
    JsonElement? ExampleSpec,
    IReadOnlyList<IReadOnlyList<string>>? OneOf = null);
