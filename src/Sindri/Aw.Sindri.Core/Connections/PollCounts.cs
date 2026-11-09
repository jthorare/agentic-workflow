using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Core.Connections;

/// <summary>What a poll of one (connection, event kind) found, for 'connection.polled'.</summary>
/// <param name="Name">The connection.</param>
/// <param name="Kind">The event kind.</param>
/// <param name="Count">How many events came back.</param>
/// <param name="Started">How many runs started or queued.</param>
/// <param name="Skipped">How many triggers were skipped.</param>
public sealed record PollCounts(ConnectionName Name, EventKind Kind, int Count, int Started, int Skipped);
