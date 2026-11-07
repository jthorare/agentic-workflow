namespace Aw.Sindri.Features.LinearConnection;

/// <summary>Where one of the two Linear keys stands (Settings, Connections, Linear).</summary>
/// <param name="State">'resolves', 'notSet' or 'refused'; for the write key also 'sameAsRead' and 'scopeUnknown'.</param>
/// <param name="CheckedAt">When the state was looked at; null while the key is not set.</param>
internal sealed record LinearKeyStateView(string State, DateTimeOffset? CheckedAt);
