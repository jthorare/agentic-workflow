namespace Aw.Sindri.Features.RenameConnection;

/// <summary>The renamed connection.</summary>
/// <param name="Name">The new name.</param>
/// <param name="PreviousName">The name before the rename.</param>
/// <param name="SecretMoved">Whether a stored secret was moved to the new name's keychain entry.</param>
public sealed record RenameConnectionResponse(string Name, string PreviousName, bool SecretMoved);
