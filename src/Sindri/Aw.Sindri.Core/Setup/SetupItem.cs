using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Setup;

/// <summary>One repo's line inside a setup step that counts repos.</summary>
/// <param name="Repo">The repo's name.</param>
/// <param name="State">Where it stands.</param>
/// <param name="Detail">What is true now, in plain words, or null.</param>
public sealed record SetupItem(string Repo, SetupState State, string? Detail)
{
    /// <summary>Each installed provider's state, for the MCP step; null (and left out of the JSON) for the other steps.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<McpProviderState>? Providers { get; init; }

    /// <inheritdoc/>
    public bool Equals(SetupItem? other) =>
        other is not null && Repo == other.Repo && State == other.State && Detail == other.Detail
        && (Providers is null ? other.Providers is null : other.Providers is not null && Providers.SequenceEqual(other.Providers));

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Repo, State, Detail);
}
