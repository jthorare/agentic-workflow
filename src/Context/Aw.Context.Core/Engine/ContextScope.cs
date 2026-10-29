using System.Text.Json;

namespace Aw.Context.Core.Engine;

/// <summary>What a facade call runs against: the approved profile's context section and repos, and who asks.</summary>
/// <param name="Settings">The profile's 'context:' section, or null when it has none.</param>
/// <param name="Repos">The profile's repos (name and checkout path).</param>
/// <param name="Caller">The caller.</param>
/// <param name="SyncConfigured">True when the host runs an intent sync (Sindri: the profile names context-sync), so an unmatched item key is explained by the state, not by a missing service.</param>
public sealed record ContextScope(JsonElement? Settings, IReadOnlyList<ContextRepo> Repos, ContextCaller Caller, bool SyncConfigured = false)
{
    /// <summary>The job this call runs for (as the host prints its id), or null when it runs for no job. A call that has to wait behind another names this job in its progress line, so a waiting job doesn't look like it runs.</summary>
    public string? Job { get; init; }
}
