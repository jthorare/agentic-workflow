using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.RenameConnection;

/// <summary>Change a connection's label ('sindri connection rename &lt;name&gt; &lt;label&gt;'; POST /sindri/connections/{name}/rename; the service part stays).</summary>
/// <param name="Name">The connection name ('slack:old').</param>
/// <param name="Label">The new label ('new'), so the connection becomes 'slack:new'.</param>
public sealed record RenameConnectionRequest(string Name, string Label) : ICommand<RenameConnectionResponse>;
