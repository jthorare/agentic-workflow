namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>GET /sindri/linear/connection (Linear hub 1b contract §4a): Settings, Connections, Linear. Owner only.</summary>
/// <param name="Read">The read key.</param>
/// <param name="Write">The write key.</param>
/// <param name="Teams">The profile's teams.</param>
/// <param name="Pending">True when the live profile differs from the approved one in this source's keys.</param>
internal sealed record LinearConnection(LinearKeyState Read, LinearKeyState Write, IReadOnlyList<LinearTeam> Teams, bool Pending);
