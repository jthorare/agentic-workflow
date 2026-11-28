using System.Text.Json.Serialization;
using Aw.Sindri.Features.AddConnection;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of PUT /sindri/connections/{name}/key: the new key itself (the CLI reads its variable and sends the value), or the name of a variable in serve's environment (an agent).</summary>
/// <param name="Key">The new key. Secret: never echoed, logged or stored in a row.</param>
/// <param name="KeyEnv">The name of the environment variable in serve's environment that holds the new key, used when 'Key' is null.</param>
internal sealed record ReplaceConnectionKeyBody(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Key = null,
    string? KeyEnv = null)
{
    /// <summary>The request for the connection the route names.</summary>
    /// <param name="name">The connection name from the route.</param>
    /// <returns>The request.</returns>
    internal ReplaceConnectionKeyRequest ToRequest(string name) => new(name, KeyEnv, Key);

    /// <summary>A fixed text: the key is not in it.</summary>
    /// <returns>'ReplaceConnectionKeyBody'.</returns>
    public override string ToString() => nameof(ReplaceConnectionKeyBody);
}
