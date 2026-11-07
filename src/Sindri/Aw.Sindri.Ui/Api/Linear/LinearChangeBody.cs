using System.Text.Json.Nodes;

namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>The body of POST /sindri/linear/issues/{key}/change: only the changed fields, under the same names in both.</summary>
/// <param name="Opened">The values the sheet opened with.</param>
/// <param name="Set">The new values (null clears assigneeId, projectId, cycleId and parentKey).</param>
internal sealed record LinearChangeBody(JsonObject Opened, JsonObject Set);
