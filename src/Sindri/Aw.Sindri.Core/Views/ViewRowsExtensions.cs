using Aw.Kernel;

namespace Aw.Sindri.Core.Views;

/// <summary>What the projections ask of <see cref="IViewRows"/> more than once.</summary>
internal static class ViewRowsExtensions
{
    /// <summary>The row under <paramref name="key"/> in <paramref name="view"/>; a row that isn't there is a fold that can't continue.</summary>
    /// <typeparam name="TRow">The row record.</typeparam>
    /// <param name="rows">The open page transaction.</param>
    /// <param name="view">A <see cref="ViewNames"/> name.</param>
    /// <param name="key">The row's key.</param>
    /// <param name="eventType">The event being folded, for the message.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The row, or SND-VIEW-001 when there is none (the projector stalls on the event).</returns>
    public static async ValueTask<Result<TRow>> Require<TRow>(this IViewRows rows, string view, string key, string eventType, CancellationToken cancellationToken)
        where TRow : class
    {
        ArgumentNullException.ThrowIfNull(rows);
        var row = await rows.Get<TRow>(view, key, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        return row.Value is { } found ? found : SindriErrors.ViewsStalled.With($"{eventType} for {key}, which has no row in {view}");
    }
}
