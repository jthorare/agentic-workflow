namespace Aw.Sindri.Ui.Api.Connections;

/// <summary>The body of POST /sindri/connections/{name}/rename.</summary>
/// <param name="Label">The new label; the service part of the name stays.</param>
internal sealed record RenameConnectionBody(string Label);
