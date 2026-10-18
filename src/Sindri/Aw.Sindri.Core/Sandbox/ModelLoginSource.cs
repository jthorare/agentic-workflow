using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Sandbox;

/// <summary>
/// Where a session's model login comes from (sandbox spec SB18, owner decision Q2). The order is the strictness order: a Step may
/// ask for a stricter source than the profile gives and never a looser one, so <see cref="OwnerCopy"/> is lower than <see cref="SeparateLogin"/>.
/// The launcher's stricter-of merge is SB-1/SB-5a; this is the vocabulary.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ModelLoginSource>))]
public enum ModelLoginSource
{
    /// <summary>The owner's own login: the agent home read-only with a named write set and a per-launch 0600 copy of the login (the default).</summary>
    [JsonStringEnumMemberName("owner-copy")]
    OwnerCopy = 0,

    /// <summary>A login only Steps use, made once with 'sindri sandbox login'; none of the owner's agent home is bound.</summary>
    [JsonStringEnumMemberName("separate-login")]
    SeparateLogin = 1,
}
