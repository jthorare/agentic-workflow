namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>One key of GET /sindri/linear/connection.</summary>
/// <param name="State">resolves, notSet, refused; for the write key also sameAsRead and scopeUnknown.</param>
/// <param name="CheckedAt">When Sindri last tried the key; null when it never has.</param>
internal sealed record LinearKeyState(string State, DateTimeOffset? CheckedAt);
