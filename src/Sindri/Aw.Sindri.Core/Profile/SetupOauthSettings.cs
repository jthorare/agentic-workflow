using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's `setup.oauth` block (first-run spec decision 15a): the client id of the OAuth app Sindri signs in through, per provider. A client id is not a secret.</summary>
/// <param name="Github">The GitHub OAuth app's client id (device flow); null: the id this build ships.</param>
/// <param name="Linear">The Linear OAuth app's client id (PKCE, loopback); null: the id this build ships.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SetupOauthSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Github = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Linear = null);
