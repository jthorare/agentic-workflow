namespace Aw.Sindri.Core.Connections;

/// <summary>
/// A connection as an answer shows it. It never carries a token, a token prefix or the secret pointer: only whether a secret is stored.
/// </summary>
/// <param name="Name">The name ('linear:work').</param>
/// <param name="Kind">The kind's wire name ('oauth').</param>
/// <param name="Preset">The preset id, or null.</param>
/// <param name="Status">The status's wire name ('connected').</param>
/// <param name="StatusReason">Why it is not connected, or null.</param>
/// <param name="Config">Non-secret settings.</param>
/// <param name="LastRefreshedAt">When the token was last refreshed, or null.</param>
/// <param name="HasSecret">Whether a secret is stored for it.</param>
public sealed record ConnectionView(
    string Name,
    string Kind,
    string? Preset,
    string Status,
    string? StatusReason,
    IReadOnlyDictionary<string, string> Config,
    DateTimeOffset? LastRefreshedAt,
    bool HasSecret)
{
    /// <summary>Shows a stored connection.</summary>
    /// <param name="connection">The connection.</param>
    /// <returns>The view.</returns>
    public static ConnectionView From(Connection connection) => throw new NotSupportedException();
}
