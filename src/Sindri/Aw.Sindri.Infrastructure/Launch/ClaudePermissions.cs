using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Claude Code's 'permissions' settings, as far as a Step's policy uses them.</summary>
/// <param name="Allow">Tool rules allowed without asking.</param>
internal sealed record ClaudePermissions(IReadOnlyList<string> Allow)
{
    /// <summary>'plan' to propose and apply nothing; absent leaves the CLI's default mode.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DefaultMode { get; init; }

    /// <summary>The one directory the Step may write, or absent.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? AdditionalDirectories { get; init; }
}
