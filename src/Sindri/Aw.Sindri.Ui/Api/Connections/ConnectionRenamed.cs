namespace Aw.Sindri.Ui.Api.Connections;

/// <summary>POST /sindri/connections/{name}/rename.</summary>
/// <param name="Name">The new name.</param>
/// <param name="PreviousName">The name before the rename.</param>
/// <param name="SecretMoved">Whether its stored secret moved to the new name's entry.</param>
internal sealed record ConnectionRenamed(string Name, string PreviousName, bool SecretMoved);
