namespace Aw.Sindri.Ui.Api.Connections;

/// <summary>GET /sindri/connections.</summary>
/// <param name="Connections">The connections.</param>
internal sealed record ConnectionList(IReadOnlyList<ConnectionView> Connections);
