
namespace Aw.Sindri.Core.Connections;

/// <summary>What a poll of one (connection, event kind) found, for 'connection.polled'.</summary>
/// <param name="Name">The connection.</param>
/// <param name="Kind">The event kind's key (EventKind.Key), so this namespace never depends on the polling one.</param>
/// <param name="Count">How many events came back.</param>
/// <param name="Started">How many runs started or queued.</param>
/// <param name="Skipped">How many triggers were skipped.</param>
public sealed record PollCounts(ConnectionName Name, string Kind, int Count, int Started, int Skipped);
