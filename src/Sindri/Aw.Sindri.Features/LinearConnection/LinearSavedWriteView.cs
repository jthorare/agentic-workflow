namespace Aw.Sindri.Features.LinearConnection;

/// <summary>Where a saved write key stands.</summary>
/// <param name="State">'resolves', or 'scopeUnknown' when Linear does not say whether the key can write.</param>
internal sealed record LinearSavedWriteView(string State);
