namespace Aw.Sindri.Ui.Api.Connections;

/// <summary>DELETE /sindri/connections/{name}.</summary>
/// <param name="Name">The removed connection.</param>
/// <param name="SecretDeleted">Whether its stored secret was deleted too.</param>
internal sealed record ConnectionRemoved(string Name, bool SecretDeleted);
