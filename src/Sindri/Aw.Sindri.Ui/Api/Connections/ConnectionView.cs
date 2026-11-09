namespace Aw.Sindri.Ui.Api.Connections;

/// <summary>One connection as the API shows it. There is no secret field: only whether one is stored.</summary>
/// <param name="Name">The name ('linear:work').</param>
/// <param name="Kind">The kind's wire name ('oauth', 'rest', 'cli', 'mcp').</param>
/// <param name="Preset">The preset id, or null.</param>
/// <param name="Status">The status's wire name ('connected', 'needs-sign-in').</param>
/// <param name="StatusReason">Why it is not connected, or null.</param>
/// <param name="Config">Non-secret settings.</param>
/// <param name="LastRefreshedAt">When the token was last refreshed, or null.</param>
/// <param name="HasSecret">Whether a secret is stored for it.</param>
internal sealed record ConnectionView(
    string Name,
    string Kind,
    string? Preset,
    string Status,
    string? StatusReason,
    IReadOnlyDictionary<string, string> Config,
    DateTimeOffset? LastRefreshedAt,
    bool HasSecret);
