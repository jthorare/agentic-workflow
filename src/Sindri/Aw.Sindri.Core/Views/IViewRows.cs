using Aw.Kernel;

namespace Aw.Sindri.Core.Views;

/// <summary>The view rows inside the open page transaction (one views store transaction per page, a savepoint per event).</summary>
public interface IViewRows
{
    /// <summary>The row under <paramref name="key"/> in <paramref name="view"/>, or null.</summary>
    /// <typeparam name="TRow">The row record.</typeparam>
    /// <param name="view">A <see cref="ViewNames"/> name.</param>
    /// <param name="key">The row's key.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The row, if any.</returns>
    public ValueTask<Result<TRow?>> Get<TRow>(string view, string key, CancellationToken cancellationToken)
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

    /// <summary>Writes <paramref name="row"/> under <paramref name="key"/> in <paramref name="view"/>.</summary>
    /// <typeparam name="TRow">The row record.</typeparam>
    /// <param name="view">A <see cref="ViewNames"/> name.</param>
    /// <param name="key">The row's key.</param>
    /// <param name="row">The row.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> Put<TRow>(string view, string key, TRow row, CancellationToken cancellationToken)
        where TRow : class;

    /// <summary>Removes the row under <paramref name="key"/> in <paramref name="view"/>, if any.</summary>
    /// <param name="view">A <see cref="ViewNames"/> name.</param>
    /// <param name="key">The row's key.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> Delete(string view, string key, CancellationToken cancellationToken);
}
