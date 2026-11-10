using Aw.Sindri.Features.RenameConnection;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/connections/{name}/rename: the new label.</summary>
/// <param name="Label">The new label ('new' makes 'slack:old' into 'slack:new').</param>
internal sealed record RenameConnectionBody(string Label)
{
    /// <summary>The request for the connection the route names.</summary>
    /// <param name="name">The connection name from the route.</param>
    /// <returns>The request.</returns>
    internal RenameConnectionRequest ToRequest(string name) => new(name, Label);
}
