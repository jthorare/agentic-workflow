using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The settings file the launcher writes for Claude Code's 'claude-settings-file' policy form: a permissions block, so it can only tighten the profile, and the two hooks that tell Sindri a turn ended or started.</summary>
/// <param name="Permissions">The permissions block.</param>
/// <param name="Hooks">The turn hooks.</param>
internal sealed record ClaudeSettingsDocument(ClaudePermissions Permissions, ClaudeHooks Hooks)
{
    /// <summary>The CLI's own sandbox switch for a sandboxed Step, or absent.</summary>
    [JsonPropertyName("sandbox")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ClaudeSandbox? Sandbox { get; init; }
}
