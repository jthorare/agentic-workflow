namespace Aw.Sindri.Features.RemoveConnection;

/// <summary>The removed connection.</summary>
/// <param name="Name">The name.</param>
/// <param name="SecretDeleted">Whether a stored secret was deleted.</param>
public sealed record RemoveConnectionResponse(string Name, bool SecretDeleted);
