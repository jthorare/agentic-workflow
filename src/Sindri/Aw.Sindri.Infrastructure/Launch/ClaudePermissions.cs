using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Claude Code's 'permissions' settings, as far as a Step's policy uses them.</summary>
/// <param name="Allow">Tool rules allowed without asking.</param>
internal sealed record ClaudePermissions(IReadOnlyList<string> Allow)
{
    /// <summary>'plan' to propose and apply nothing; absent leaves the CLI's default mode.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DefaultMode { get; init; }

    /// <summary>Tool rules that are always refused (the Step's denied paths, a read-only path's edits), or absent.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Deny { get; init; }

    /// <summary>The directories the Step may use: the one it writes and the read-only ones, or absent.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? AdditionalDirectories { get; init; }
}
