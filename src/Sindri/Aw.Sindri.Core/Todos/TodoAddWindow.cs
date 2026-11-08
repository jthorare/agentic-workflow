using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Todos;

/// <summary>
/// What the ledger says about adds (todo spec §4.4 "The hourly limit"): which client ids already made a todo, and the adds of the last hour by who wrote them. Read from the ledger
/// (so it survives a restart) and kept up to date by reading only what was appended since the last look. One per process; the adds of one actor run inside <see cref="Lock"/>.
/// </summary>
public sealed class TodoAddWindow
{
    /// <summary>How long an add counts against the hourly limit.</summary>
    public static readonly TimeSpan Span = TimeSpan.FromHours(1);

    private const int Page = 1000;

    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Earlier> _byClient = new(StringComparer.Ordinal);
    private readonly List<Recent> _recent = [];
    private long _scannedTo;

    /// <summary>One todo a client id made.</summary>
    /// <param name="Id">The todo.</param>
    /// <param name="Sequence">The ledger sequence of its 'todo.added'.</param>
    /// <param name="Due">Its due date when added.</param>
    public sealed record Earlier(TodoId Id, long Sequence, DateOnly? Due);

    /// <summary>One add inside the hour.</summary>
    /// <param name="At">When it was recorded.</param>
    /// <param name="By">Who wrote it.</param>
    /// <param name="Via">Its 'via'.</param>
    public sealed record Recent(DateTimeOffset At, string By, string? Via);

    /// <summary>The lock that serializes the adds of <paramref name="actor"/>, so counting and appending never interleave.</summary>
    /// <param name="actor">The 'by' of the caller.</param>
    /// <returns>The semaphore.</returns>
    public SemaphoreSlim Lock(string actor) => _locks.GetOrAdd(actor, static _ => new SemaphoreSlim(1, 1));

    /// <summary>Reads what was appended since the last look.</summary>
    /// <param name="ledger">The ledger.</param>
    /// <param name="events">Reads the payloads.</param>
    /// <param name="now">The clock: adds older than the hour are not kept.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Unit, or the ledger's error.</returns>
    public async ValueTask<Result<Unit>> Refresh(ILedgerStore ledger, EventFactory events, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            while (true)
            {
                var page = await ledger.ReadAfter(_scannedTo, Page, cancellationToken).ConfigureAwait(false);
                if (page.IsError)
                {
                    return page.Error;
                }

                foreach (var recorded in page.Value)
                {
                    _scannedTo = recorded.Sequence;
                    if (string.Equals(recorded.Envelope.Type, EventTypes.TodoAdded, StringComparison.Ordinal) && events.Payload<TodoAdded>(recorded) is { IsSuccess: true } read)
                    {
                        Note(recorded, read.Value, now);
                    }
                }

                if (page.Value.Count < Page)
                {
                    return Unit.Value;
                }
            }
        }
        finally
        {
            _ = _sync.Release();
        }
    }

    /// <summary>The todo an earlier add with this client id made.</summary>
    /// <param name="clientId">The client id.</param>
    /// <returns>The todo, or null when none did.</returns>
    public Earlier? Find(TodoClientId clientId)
    {
        lock (_byClient)
        {
            return _byClient.GetValueOrDefault(clientId.Value);
        }
    }

    /// <summary>The adds inside the hour before <paramref name="now"/> that <paramref name="by"/> made, oldest first; a pipe-only count keeps just the ones recorded with 'via: pipe'.</summary>
    /// <param name="by">Who wrote them.</param>
    /// <param name="pipeOnly">Whether only piped adds count.</param>
    /// <param name="now">The clock.</param>
    /// <returns>The adds.</returns>
    public IReadOnlyList<Recent> Within(string by, bool pipeOnly, DateTimeOffset now)
    {
        lock (_recent)
        {
            _ = _recent.RemoveAll(r => now - r.At >= Span);
            return [.. _recent.Where(r => string.Equals(r.By, by, StringComparison.Ordinal) && (!pipeOnly || string.Equals(r.Via, TodoWrite.Pipe, StringComparison.Ordinal)))];
        }
    }

    private void Note(LedgerEvent recorded, TodoAdded added, DateTimeOffset now)
    {
        if (added.ClientId is { } client)
        {
            lock (_byClient)
            {
                _ = _byClient.TryAdd(client.Value, new Earlier(added.Id, recorded.Sequence, added.Due));
            }
        }

        if (now - recorded.Envelope.OccurredAt >= Span)
        {
            return;
        }

        lock (_recent)
        {
            _recent.Add(new Recent(recorded.Envelope.OccurredAt, added.By, added.Via));
        }
    }
}
