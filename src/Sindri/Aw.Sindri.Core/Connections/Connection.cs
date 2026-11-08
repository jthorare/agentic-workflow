using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Core.Connections;

/// <summary>
/// One stored connection: non-secret config and a pointer to the secret. The record has no secret field, so nothing that
/// prints, logs or serializes it can leak one.
/// </summary>
/// <param name="Name">The name ('linear:work').</param>
/// <param name="Kind">What it signs in to.</param>
/// <param name="Preset">The preset id ('linear'), or null for a hand-made connection.</param>
/// <param name="Config">Non-secret settings (client id, scopes, probe URL).</param>
/// <param name="SecretPointer">Where the secret is ('keychain:sindri-connection/linear.work'), or null before sign-in finished.</param>
/// <param name="Status">Where it stands.</param>
/// <param name="StatusReason">Why it is not connected, or null.</param>
/// <param name="CreatedBy">Who added it (a caller label).</param>
/// <param name="CreatedAt">When it was added.</param>
/// <param name="UpdatedAt">When the row last changed.</param>
/// <param name="LastRefreshedAt">When the token was last refreshed, or null.</param>
public sealed record Connection(
    ConnectionName Name,
    ConnectionKind Kind,
    string? Preset,
    IReadOnlyDictionary<string, string> Config,
    SecretPointer? SecretPointer,
    ConnectionStatus Status,
    string? StatusReason,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastRefreshedAt);
