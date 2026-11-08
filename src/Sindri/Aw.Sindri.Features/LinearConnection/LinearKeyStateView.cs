namespace Aw.Sindri.Features.LinearConnection;

/// <summary>Where one of the two Linear keys stands (Settings, Connections, Linear).</summary>
/// <param name="State">'resolves', 'notSet', 'refused' or 'unchecked' (a pointer is set but no save checked it, or the profile's pointer changed since); for the write key also 'sameAsRead' and 'scopeUnknown'.</param>
/// <param name="CheckedAt">When the last save checked the key; null while the key is not set, or was never checked, and for a state read off the profile alone ('refused', 'sameAsRead'). It moves only on a save.</param>
internal sealed record LinearKeyStateView(string State, DateTimeOffset? CheckedAt);
