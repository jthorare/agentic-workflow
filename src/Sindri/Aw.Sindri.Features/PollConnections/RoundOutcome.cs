using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>What one provider call did.</summary>
/// <param name="Cursor">The cursor as it stands after the call (saved when the call succeeded).</param>
/// <param name="Keys">The delivery ids the provider returned.</param>
/// <param name="Started">Runs started or queued.</param>
/// <param name="Skipped">Triggers skipped (a run in flight, or the delivery already had its run).</param>
/// <param name="CursorMoved">True when the saved cursor value changed.</param>
/// <param name="RateLimited">True when the provider asked to wait; the backoff is saved and nothing else changed.</param>
/// <param name="Gap">True when the poller hit its own page cap and skipped older events.</param>
/// <param name="Reason">The poller's reason for the gap, or null.</param>
internal sealed record RoundOutcome(PollCursor Cursor, IReadOnlyList<string> Keys, int Started, int Skipped, bool CursorMoved, bool RateLimited, bool Gap, string? Reason);
