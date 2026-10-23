using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Launch;

/// <summary>What owns a launched agent process (launcher spec SL4).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HostKind>))]
public enum HostKind
{
    /// <summary>A session on the Sindri-owned tmux server (-L sindri-launch).</summary>
    [JsonStringEnumMemberName("tmux")]
    Tmux,

    /// <summary>No host: the viewer owns the process (reserved; attended opens refuse until SL-4).</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>A T3 Code thread in the item's worktree; the T3 app shows it (attended opens only).</summary>
    [JsonStringEnumMemberName("t3")]
    T3,
}
