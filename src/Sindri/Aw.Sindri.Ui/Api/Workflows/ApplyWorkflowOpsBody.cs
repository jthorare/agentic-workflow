using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>The body of POST /sindri/workflows/{slug}/ops. Both fields are always written.</summary>
/// <param name="BaseRevision">The newest revision the editor read before it made these ops.</param>
/// <param name="Ops">The ops, a JSON array of objects with an "op" member (set_config, add_node, connect, remove_node).</param>
internal sealed record ApplyWorkflowOpsBody(int BaseRevision, JsonElement Ops);
