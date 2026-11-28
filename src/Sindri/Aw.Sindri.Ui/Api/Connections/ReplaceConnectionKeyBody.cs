using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Connections;

/// <summary>PUT /sindri/connections/{name}/key's body: the new key, sent in this one request and never shown by <see cref="ToString"/> (#4358).</summary>
/// <param name="Key">The new key itself. Secret.</param>
internal sealed record ReplaceConnectionKeyBody([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Key)
{
    /// <summary>A fixed text: the key is not in it.</summary>
    /// <returns>'ReplaceConnectionKeyBody'.</returns>
    public override string ToString() => nameof(ReplaceConnectionKeyBody);
}
