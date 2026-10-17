using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Launch;

/// <summary>How a catalog entry takes a Step's model id (launcher L-17); the argument is added only on an unattended launch.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ModelForm>))]
public enum ModelForm
{
    /// <summary>The entry can't take a model; a Step that asks for one is SND-LAUNCH-013.</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>'--model &lt;id&gt;'.</summary>
    [JsonStringEnumMemberName("claude-flag")]
    ClaudeFlag,

    /// <summary>'-m &lt;id&gt;'.</summary>
    [JsonStringEnumMemberName("codex-flag")]
    CodexFlag,

    /// <summary>'--model &lt;id&gt;' (unverified: no cursor agent binary on the build box).</summary>
    [JsonStringEnumMemberName("cursor-flag")]
    CursorFlag,
}
