using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Launch;

/// <summary>How a catalog entry takes a Step's policy (launcher L-17); the arguments are added only on an unattended launch.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PolicyForm>))]
public enum PolicyForm
{
    /// <summary>The entry can't express a policy; a Step that asks for one is SND-LAUNCH-013.</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>'--settings &lt;path&gt;' to a 0600 settings file the launcher writes from the <see cref="StepPolicy"/> and deletes with the launch.</summary>
    [JsonStringEnumMemberName("claude-settings-file")]
    ClaudeSettingsFile,

    /// <summary>Typed '-c key=value' overrides the launcher renders from the <see cref="StepPolicy"/>.</summary>
    [JsonStringEnumMemberName("codex-overrides")]
    CodexOverrides,

    /// <summary>A cursor config file (unverified; treated as unable to express it until it is).</summary>
    [JsonStringEnumMemberName("cursor-config")]
    CursorConfig,
}
