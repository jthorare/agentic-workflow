using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The window of the profile key 'workflow.scoping.historyWindow': the form the history input's 'window' argument takes. An empty window is the open one (all history).</summary>
/// <param name="Since">The earliest instant kept (RFC 3339 or a date YYYY-MM-DD).</param>
/// <param name="Before">The latest instant (RFC 3339 or a date YYYY-MM-DD).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HistoryWindowSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Since = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Before = null);
