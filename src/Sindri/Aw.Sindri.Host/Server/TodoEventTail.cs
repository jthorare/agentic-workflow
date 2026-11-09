using System.Text.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Host.Server;

/// <summary>Follows the ledger for the todo note watch: waits for an append, then says whether a todo event another writer made came in (the note's own writes, 'by' obsidian or service:todo-note, don't count).</summary>
/// <param name="ledger">Reads the events.</param>
/// <param name="signal">Wakes on an append.</param>
internal sealed class TodoEventTail(ILedgerReader ledger, ILedgerSignal signal)
{
    private const int Batch = 200;

    private long _cursor = -1;

    /// <summary>Starts at the ledger's head, once.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the cursor is set.</returns>
    public async ValueTask Start(CancellationToken cancellationToken)
    {
        if (_cursor < 0)
        {
            var head = await ledger.Head(cancellationToken).ConfigureAwait(false);
            _cursor = head.IsSuccess ? head.Value : 0;
        }
    }

    /// <summary>Waits for an append past the cursor, or <paramref name="limit"/>.</summary>
    /// <param name="limit">The longest wait.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The wait.</returns>
    public Task Wait(TimeSpan limit, CancellationToken cancellationToken) => signal.WaitAfter(_cursor, limit, cancellationToken).AsTask();

    /// <summary>Reads the events past the cursor.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when one is a todo event another writer made.</returns>
    public async ValueTask<bool> Foreign(CancellationToken cancellationToken)
    {
        var found = false;
        while (true)
        {
            var read = await ledger.ReadAfter(_cursor, Batch, cancellationToken).ConfigureAwait(false);
            if (read.IsError || read.Value.Count == 0)
            {
                return found;
            }

            _cursor = read.Value[^1].Sequence;
            found |= read.Value.Any(IsForeign);
            if (read.Value.Count < Batch)
            {
                return found;
            }
        }
    }

    private static bool IsForeign(LedgerEvent e) =>
        e.Envelope.Type.StartsWith("todo.", StringComparison.Ordinal) && !string.Equals(e.Envelope.Type, EventTypes.TodoNoteSynced, StringComparison.Ordinal) && !WrittenByTheSync(e.Envelope.Payload);

    private static bool WrittenByTheSync(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var by = doc.RootElement.EnumerateObject().FirstOrDefault(static p => string.Equals(p.Name, "by", StringComparison.OrdinalIgnoreCase));
            var who = by.Value.ValueKind == JsonValueKind.String ? by.Value.GetString() : null;
            return string.Equals(who, "obsidian", StringComparison.Ordinal) || string.Equals(who, "service:todo-note", StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;   // boundary: a payload that doesn't parse is not ours; a pass is cheap
        }
    }
}
