using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Kernel;

namespace Aw.Evidence.Testing;

/// <summary>
/// A scripted IDatabaseReader: a statement is answered with the rows it was given (cut to the row limit like the real readers), refused,
/// or failed; a second statement is refused; any other statement fails as an unknown table. Every target opened and every statement
/// run is recorded, so a test can show what did and did not reach the database.
/// </summary>
public sealed class FakeDatabaseReader : IDatabaseReader
{
    private readonly Dictionary<string, Rows> _answers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _refused = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _failing = new(StringComparer.Ordinal);
    private readonly List<DbTarget> _opened = [];
    private readonly List<string> _queries = [];

    /// <summary>Every target opened, in order.</summary>
    public IReadOnlyList<DbTarget> Opened => _opened;

    /// <summary>Every statement run against any session, in order (a refused one is included).</summary>
    public IReadOnlyList<string> Queries => _queries;

    /// <summary>When set, <see cref="OpenAsync"/> returns this error and no query can run.</summary>
    public Error? OpenError { get; set; }

    /// <summary>Answers <paramref name="sql"/> with <paramref name="rows"/>.</summary>
    /// <param name="sql">The statement.</param>
    /// <param name="rows">All the rows; the session keeps as many as the limit allows.</param>
    /// <returns>This fake.</returns>
    public FakeDatabaseReader Answer(string sql, Rows rows)
    {
        _answers[sql] = rows;
        return this;
    }

    /// <summary>Makes the connection refuse <paramref name="sql"/>, as a read-only connection refuses a write.</summary>
    /// <param name="sql">The statement.</param>
    /// <returns>This fake.</returns>
    public FakeDatabaseReader Refuse(string sql)
    {
        _ = _refused.Add(sql);
        return this;
    }

    /// <summary>Makes <paramref name="sql"/> fail for a reason other than a refusal.</summary>
    /// <param name="sql">The statement.</param>
    /// <param name="message">The failure's message.</param>
    /// <returns>This fake.</returns>
    public FakeDatabaseReader Fail(string sql, string message)
    {
        _failing[sql] = message;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadSession>> OpenAsync(DbTarget target, CancellationToken cancellationToken)
    {
        _opened.Add(target);
        return ValueTask.FromResult<Result<IReadSession>>(OpenError is { } error ? error : new Session(this));
    }

    private Result<Rows> Run(string sql, int rowLimit)
    {
        _queries.Add(sql);
        if (_refused.Contains(sql) || SqlText.Statements(sql, nestedComments: true).Count > 1)
        {
            return EvidenceErrors.DatabaseRefused.With($"the database refused a write: {sql}");
        }

        if (_failing.TryGetValue(sql, out var message))
        {
            return EvidenceErrors.RunFailed.With(message);
        }

        if (!_answers.TryGetValue(sql, out var rows))
        {
            return EvidenceErrors.RunFailed.With($"no such table (nothing answers {sql})");
        }

        return rows.Count <= rowLimit ? rows : rows with { Values = [.. rows.Values.Take(rowLimit)], Truncated = true };
    }

    private sealed class Session(FakeDatabaseReader owner) : IReadSession
    {
        public ValueTask<Result<Rows>> QueryAsync(string sql, int rowLimit, CancellationToken cancellationToken) =>
            ValueTask.FromResult(owner.Run(sql, rowLimit));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
