using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Sindri.Infrastructure.Views;

/// <summary>
/// The rows one projector page has read and written, held in memory until the page ends (#2890). A 100k catch-up page re-reads and
/// re-writes the same few dozen item and job rows several times each; here each row is read from views.db at most once and written
/// at most once per page. An event's writes are staged apart and join the page's only when the event folds cleanly, so a failing
/// event is still undone alone. A Where query reads views.db, so the view's staged rows are written first: the folded events' rows
/// before the event's savepoint opens, the event's own under it.
/// </summary>
/// <param name="session">The page transaction, through its statement cache.</param>
internal sealed class PageRows(SqliteStatementCache session)
{
    private readonly Dictionary<(string View, string Key), string?> _stored = [];
    private readonly Dictionary<(string View, string Key), Staged> _folded = [];
    private readonly Dictionary<(string View, string Key), Staged> _event = [];
    private EventSavepoint? _savepoint;

    /// <summary>The page transaction.</summary>
    internal SqliteStatementCache Session { get; } = session;

    /// <summary>Starts an event: its writes are staged apart until <see cref="Keep"/> or <see cref="Drop"/>.</summary>
    /// <param name="savepoint">The event's savepoint, opened only if the event's own rows must reach views.db before the page ends.</param>
    internal void Begin(EventSavepoint savepoint)
    {
        _event.Clear();
        _savepoint = savepoint;
    }

    /// <summary>The row as this page sees it, when the page has read or written it; <paramref name="text"/> is null for no row.</summary>
    internal bool TryRead(string view, string key, out string? text)
    {
        if (_event.TryGetValue((view, key), out var staged) || _folded.TryGetValue((view, key), out staged))
        {
            text = staged.Text;
            return true;
        }

        return _stored.TryGetValue((view, key), out text);
    }

    /// <summary>Remembers what views.db holds for a row the page read.</summary>
    internal void Read(string view, string key, string? text) => _stored[(view, key)] = text;

    /// <summary>Stages the event's write of a row; a null <paramref name="text"/> deletes it.</summary>
    internal void Stage(string view, string key, string? text, long sequence) => _event[(view, key)] = new Staged(text, sequence);

    /// <summary>The event folded cleanly: its rows join the page's.</summary>
    internal void Keep()
    {
        foreach (var (id, staged) in _event)
        {
            _folded[id] = staged;
        }

        _event.Clear();
        _savepoint = null;
    }

    /// <summary>The event failed: its staged rows are dropped (its savepoint, if opened, is rolled back by the caller).</summary>
    internal void Drop()
    {
        _event.Clear();
        _savepoint = null;
    }

    /// <summary>Writes every staged row of <paramref name="view"/> so a query of views.db sees them.</summary>
    internal async ValueTask<Result<Unit>> Flush(string view, CancellationToken cancellationToken)
    {
        var folded = await Write(_folded, id => true, cancellationToken).ConfigureAwait(false);
        if (folded.IsError || !_event.Keys.Any(id => id.View == view))
        {
            return folded;
        }

        if (_savepoint is not null)
        {
            await _savepoint.Open(cancellationToken).ConfigureAwait(false);
        }

        return await Write(_event, id => id.View == view, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes the folded events' rows; called when the page ends, after a failing event's savepoint is rolled back.</summary>
    internal ValueTask<Result<Unit>> Finish(CancellationToken cancellationToken) => Write(_folded, id => true, cancellationToken);

    private async ValueTask<Result<Unit>> Write(Dictionary<(string View, string Key), Staged> rows, Func<(string View, string Key), bool> which,
        CancellationToken cancellationToken)
    {
        foreach (var id in rows.Keys.Where(which).ToList())
        {
            var staged = rows[id];
            if (ViewSql.Table(id.View) is not { } table)
            {
                return SqliteViewRows.Unknown(id.View);
            }

            var statement = staged.Text is { } text
                ? new SqlStatement(table.Upsert, c => c.Bind("$key", id.Key).Bind("$row", text).Bind("$seq", staged.Sequence))
                : new SqlStatement(table.Delete, c => c.Bind("$key", id.Key));
            var written = await SqliteQueries.Execute(Session, statement, cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return ViewsDatabase.Translate(written.Error);
            }

            _stored[id] = staged.Text;
            _ = rows.Remove(id);
        }

        return Unit.Value;
    }

    private readonly record struct Staged(string? Text, long Sequence);
}
