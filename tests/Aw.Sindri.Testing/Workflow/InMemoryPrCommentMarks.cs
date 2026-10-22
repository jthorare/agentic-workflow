using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Effects;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>The in-memory <see cref="IPrCommentMarks"/>: an append-only list. Passes <see cref="PrCommentMarksContract"/>.</summary>
public sealed class InMemoryPrCommentMarks : IPrCommentMarks
{
    private readonly List<PrCommentMark> _rows = [];
    private readonly Lock _gate = new();

    /// <inheritdoc/>
    public ValueTask<Result<bool>> Write(PrCommentMark mark, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mark);
        lock (_gate)
        {
            if (_rows.Exists(r => string.Equals(r.Effect, mark.Effect, StringComparison.Ordinal)))
            {
                return ValueTask.FromResult<Result<bool>>(false);
            }

            _rows.Add(mark);
            return ValueTask.FromResult<Result<bool>>(true);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<PrCommentMark>>> Latest(PrRef pr, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var latest = new List<PrCommentMark>();
            foreach (var row in _rows.Where(r => r.Pr == pr))
            {
                var at = latest.FindIndex(l => l.CommentId == row.CommentId && string.Equals(l.EditedAt, row.EditedAt, StringComparison.Ordinal));
                if (at < 0)
                {
                    latest.Add(row);
                }
                else if (row.At >= latest[at].At)
                {
                    latest[at] = row;   // the later write wins a tie
                }
            }

            return ValueTask.FromResult<Result<IReadOnlyList<PrCommentMark>>>(latest);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<int>> Purge(PrRef pr, bool anotherInstanceNamesPr, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<Result<int>>(anotherInstanceNamesPr ? 0 : _rows.RemoveAll(r => r.Pr == pr));
        }
    }
}
