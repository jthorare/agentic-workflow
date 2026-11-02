using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's 'workflow.scoping:' block (the scoping recipe's keys).</summary>
/// <param name="HistoryWindow">The window the scoping recipe's history input reads (<see cref="HistoryWindowSettings"/>); left out, the history is read whole.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ScopingSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] HistoryWindowSettings? HistoryWindow = null);
