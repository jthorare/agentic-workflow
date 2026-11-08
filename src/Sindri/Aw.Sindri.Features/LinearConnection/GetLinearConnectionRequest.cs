using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>Where the Linear connection stands (Linear hub spec 2026-11-04 §3, Settings, Connections, Linear). Owner only.</summary>
internal sealed record GetLinearConnectionRequest : IQuery<LinearConnectionView>;
