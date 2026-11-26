using System.Text.Json.Serialization;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.AddConnection;

/// <summary>
/// Give a connected key connection a new key without removing it ('sindri connection set-key &lt;name&gt; --key-env NAME'; PUT /sindri/connections/{name}/key; MCP connection_set_key). The new key is checked
/// like a new one; the old key stays in the keychain until the new one passes. The answer is an <see cref="AddConnectionResponse"/> whose consent address is always null.
/// </summary>
/// <remarks>The CLI reads --key-env in its own process and sends the VALUE in 'Key'; an agent sends only the variable's name in 'KeyEnv', read from serve's environment. 'Key' is never echoed, logged or stored in a row.</remarks>
/// <param name="Name">The connection name ('linear:work').</param>
/// <param name="KeyEnv">The name of the environment variable that holds the new key (read from serve's environment when 'Key' is null).</param>
/// <param name="Key">The new key itself, read by the CLI from its own environment. Secret.</param>
public sealed record ReplaceConnectionKeyRequest(
    string Name,
    string? KeyEnv = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Key = null) : ICommand<AddConnectionResponse>
{
    /// <summary>A fixed text with the name only: the key is not in it.</summary>
    /// <returns>'ReplaceConnectionKeyRequest(&lt;name&gt;)'.</returns>
    public override string ToString() => $"ReplaceConnectionKeyRequest({Name})";
}
