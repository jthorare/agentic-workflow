using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Sandbox.Credentials;

/// <summary>What a credential source finds for a target (sandbox spec SB18).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LoginState>))]
public enum LoginState
{
    /// <summary>A login is there and usable.</summary>
    [JsonStringEnumMemberName("ready")]
    Ready,

    /// <summary>No login for the target.</summary>
    [JsonStringEnumMemberName("missing")]
    Missing,

    /// <summary>The login expired.</summary>
    [JsonStringEnumMemberName("expired")]
    Expired,
}
