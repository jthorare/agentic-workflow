namespace Aw.Sindri.Features.PollConnections;

/// <summary>What one read of a (connection, event kind) did, summed over its provider calls.</summary>
/// <param name="Events">Events the provider returned, each delivery id counted once.</param>
/// <param name="Started">Runs started or queued.</param>
/// <param name="Skipped">Triggers skipped.</param>
/// <param name="Capped">True when the read stopped at a cap and recorded catchup.capped.</param>
/// <param name="RateLimited">True when the provider asked to wait.</param>
internal sealed record ReadOutcome(int Events, int Started, int Skipped, bool Capped, bool RateLimited);
