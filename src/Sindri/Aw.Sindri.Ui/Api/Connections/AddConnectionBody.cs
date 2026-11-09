using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Connections;

/// <summary>
/// POST /sindri/connections's body. 'Key' and 'ClientSecret' are the pasted secrets: sent in this one request, left out of the JSON when null,
/// and never shown by <see cref="ToString"/>.
/// </summary>
/// <param name="Name">The connection name ('linear:work').</param>
/// <param name="Preset">The preset id, or null.</param>
/// <param name="Kind">The kind's wire name; null takes the preset's.</param>
/// <param name="Client">The OAuth client id, or null.</param>
/// <param name="KeyEnv">The name of the environment variable that holds the key, or null.</param>
/// <param name="Scopes">The scopes; empty takes the preset's.</param>
/// <param name="Wait">True waits for the callback; false returns the consent URL at once.</param>
/// <param name="ClientSecretEnv">The name of the environment variable that holds the client secret, or null.</param>
/// <param name="Key">The key itself. Secret.</param>
/// <param name="ClientSecret">The OAuth client secret itself. Secret.</param>
/// <param name="Url">The mcp server's URL, or null.</param>
internal sealed record AddConnectionBody(
    string Name,
    string? Preset,
    string? Kind,
    string? Client,
    string? KeyEnv,
    IReadOnlyList<string> Scopes,
    bool Wait,
    string? ClientSecretEnv = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Key = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ClientSecret = null,
    string? Url = null)
{
    /// <summary>A fixed text with the name only: the key and the client secret are not in it.</summary>
    /// <returns>'AddConnectionBody(&lt;name&gt;)'.</returns>
    public override string ToString() => $"AddConnectionBody({Name})";
}
