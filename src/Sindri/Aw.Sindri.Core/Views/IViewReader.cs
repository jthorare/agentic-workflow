using Aw.Kernel;

namespace Aw.Sindri.Core.Views;

/// <summary>The queries' read port over the views (a read-only connection); it never creates the views store.</summary>
public interface IViewReader
{
    /// <summary>The row under <paramref name="key"/> in <paramref name="view"/>, or null.</summary>
    /// <typeparam name="TRow">The row record.</typeparam>
    /// <param name="view">A <see cref="ViewNames"/> name.</param>
    /// <param name="key">The row's key.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The row, if any.</returns>
    public ValueTask<Result<TRow?>> Get<TRow>(string view, string key, CancellationToken cancellationToken)
        where TRow : class;

    /// <summary>Every row in <paramref name="view"/>, ordered by key.</summary>
    /// <typeparam name="TRow">The row record.</typeparam>
    /// <param name="view">A <see cref="ViewNames"/> name.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The rows.</returns>
    public ValueTask<Result<IReadOnlyList<TRow>>> List<TRow>(string view, CancellationToken cancellationToken)
        where TRow : class;

    /// <summary>The rows of <paramref name="view"/> whose keys sort after <paramref name="afterKey"/> and before <paramref name="beforeKey"/> (both exclusive), in key order, at most <paramref name="max"/>.</summary>
    /// <typeparam name="TRow">The row record.</typeparam>
    /// <param name="view">A <see cref="ViewNames"/> name.</param>
    /// <param name="afterKey">The lower bound (exclusive).</param>
    /// <param name="beforeKey">The upper bound (exclusive), or null for none.</param>
    /// <param name="max">The most rows to return.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The rows.</returns>
    public ValueTask<Result<IReadOnlyList<TRow>>> Range<TRow>(string view, string afterKey, string? beforeKey, int max, CancellationToken cancellationToken)
        where TRow : class;

    /// <summary>The rows of <paramref name="view"/> whose <paramref name="column"/> equals <paramref name="value"/>, ordered by key.</summary>
    /// <typeparam name="TRow">The row record.</typeparam>
    /// <param name="view">A <see cref="ViewNames"/> name.</param>
    /// <param name="column">The column to match; the view must index it (the requests and decisions views: item, state, repo; the needs-you view: item, repo, kind).</param>
    /// <param name="value">The value, in its wire form.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The rows, or SND-CLI-003 when <paramref name="view"/> doesn't index <paramref name="column"/>.</returns>
    public ValueTask<Result<IReadOnlyList<TRow>>> Where<TRow>(string view, ViewColumn column, string value, CancellationToken cancellationToken)
        where TRow : class;

    /// <summary>The projector_state row, or null when the views store doesn't exist (doctor reads it while stopped).</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The state, if any.</returns>
    public ValueTask<Result<ProjectorState?>> State(CancellationToken cancellationToken);
}
