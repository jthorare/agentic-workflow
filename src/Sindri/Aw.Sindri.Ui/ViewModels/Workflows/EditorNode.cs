using System.Text.Json;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One node of the workflow the editor has read: what its form needs. The config stays raw JSON; the form shows the fields its block names.</summary>
/// <param name="Id">The node's id.</param>
/// <param name="Type">The block type, such as "run_agent".</param>
/// <param name="Title">The node's own title, or null.</param>
/// <param name="Config">The node's config object, or a default (undefined) element when it has none.</param>
internal sealed record EditorNode(string Id, string Type, string? Title, JsonElement Config);
