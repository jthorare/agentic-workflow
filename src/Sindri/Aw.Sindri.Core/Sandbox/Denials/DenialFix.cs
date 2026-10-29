using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Sandbox.Denials;

/// <summary>What the owner can do about one denial (sandbox spec §3.6, fix by kind).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DenialFix>))]
public enum DenialFix
{
    /// <summary>A button: allow the host, or the home path, for this repo.</summary>
    [JsonStringEnumMemberName("allow")]
    Allow,

    /// <summary>No button: the reason says why it is never allowed.</summary>
    [JsonStringEnumMemberName("never")]
    Never,

    /// <summary>No button: do it from your own terminal (a tool the inner policy refused, such as 'git push').</summary>
    [JsonStringEnumMemberName("own-terminal")]
    OwnTerminal,
}
