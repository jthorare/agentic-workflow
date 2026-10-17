using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Launch;

/// <summary>How much a Step may do without asking (launcher L-17(a)). Bypass and full access are not modes: a Step only tightens the profile.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StepPermissionMode>))]
public enum StepPermissionMode
{
    /// <summary>Reads only; nothing is written, run or pushed.</summary>
    [JsonStringEnumMemberName("read-only")]
    ReadOnly,

    /// <summary>The CLI's plan mode: it proposes, and nothing is applied.</summary>
    [JsonStringEnumMemberName("plan")]
    Plan,

    /// <summary>The CLI's default mode, writing only <see cref="StepPolicy.WritablePath"/>.</summary>
    [JsonStringEnumMemberName("default")]
    Default,
}
