using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// A repo's learn-mode history, read from the ledger (sandbox spec §3.6, Q13): never views.db, whose projector may lag, so the plan builder closes the window on what was recorded.
/// 'launch.started' names an item, not a repo, so the repo's items come first from 'item.created'; the runs are then the repo's 'launch.started' rows whose sandbox summary ran in
/// learn mode. The window ends at the latest 'sandbox.learn-ended' for the repo or for every repo.
/// </summary>
/// <param name="ledger">The ledger, read-only.</param>
/// <param name="events">Reads a payload.</param>
internal sealed class LedgerLearnHistory(ILedgerReader ledger, EventFactory events) : ILearnHistory
{
    /// <summary>What an unreadable ledger reads as: a window that cannot be proven open is closed (the run cap is spent), never an empty history that would open it.</summary>
    private static readonly LearnHistory _unreadable = new(FirstLearnRun: null, LearnRuns: int.MaxValue, EndedAt: null);

    public async ValueTask<LearnHistory> Read(string repo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var created = await Rows(EventTypes.ItemCreated, cancellationToken).ConfigureAwait(false);
        var started = await Rows(EventTypes.LaunchStarted, cancellationToken).ConfigureAwait(false);
        var ended = await Rows(EventTypes.SandboxLearnEnded, cancellationToken).ConfigureAwait(false);
        if (created.IsError || started.IsError || ended.IsError)
        {
            return _unreadable;
        }

        var learnRuns = LearnRuns(started.Value, ItemsOf(created.Value, repo));
        var ends = Ends(ended.Value, repo);
        return new LearnHistory(learnRuns.Count == 0 ? null : learnRuns.Min(), learnRuns.Count, ends.Count == 0 ? null : ends.Max());
    }

    /// <summary>The ids of the repo's items.</summary>
    private HashSet<string> ItemsOf(List<LedgerEvent> created, string repo) =>
        created
            .Select(events.Payload<ItemCreated>)
            .Where(p => p.IsSuccess && p.Value.Repo is { } own && string.Equals(own.Value, repo, StringComparison.Ordinal))
            .Select(static p => p.Value.Item.Value)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>When each launch of one of the items started in learn mode.</summary>
    private List<DateTimeOffset> LearnRuns(List<LedgerEvent> started, HashSet<string> items) =>
        [.. started
            .Where(row => row.Item is { } item && items.Contains(item)
                && events.Payload<LaunchStarted>(row) is { IsSuccess: true, Value.Sandbox.NetworkMode: NetworkMode.Learn })
            .Select(static row => row.Envelope.OccurredAt)];

    /// <summary>When learn mode was ended for the repo or for every repo.</summary>
    private List<DateTimeOffset> Ends(List<LedgerEvent> ended, string repo) =>
        [.. ended
            .Select(events.Payload<SandboxLearnEnded>)
            .Where(p => p.IsSuccess && (p.Value.Repo is not { } scope || string.Equals(scope.Value, repo, StringComparison.Ordinal)))
            .Select(static p => p.Value.At)];

    /// <summary>Every row of one type, oldest first.</summary>
    private ValueTask<Result<List<LedgerEvent>>> Rows(string type, CancellationToken cancellationToken) => LedgerRows.Of(ledger, type, cancellationToken);
}
