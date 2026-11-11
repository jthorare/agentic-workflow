using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>One group the service decided to read: its saved cursor and how.</summary>
/// <param name="Group">The group.</param>
/// <param name="Cursor">The saved cursor, or null when the group was never polled.</param>
/// <param name="CatchUp">True when the read goes forward (control-plane start or wake).</param>
/// <param name="Pages">How many provider calls a forward read makes at most.</param>
internal sealed record GroupRead(PollGroup Group, PollCursor? Cursor, bool CatchUp, int Pages);
