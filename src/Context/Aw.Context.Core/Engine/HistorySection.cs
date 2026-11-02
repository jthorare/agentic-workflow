namespace Aw.Context.Core.Engine;

/// <summary>The profile's 'context.history' keys as written: the opt-in deadline of the git-history read.</summary>
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
internal sealed record HistorySection(string? Deadline = null);
