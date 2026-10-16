using Aw.Kernel;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Tests.ListInstances;

/// <summary>
/// The 'items' view with fixed rows, for the list's title lookup. It answers keyed reads only: a whole-view list is a test failure
/// (the list service reads the items its instances are on, not every item). With <paramref name="failing"/> every read is an error.
/// </summary>
internal sealed class FixedItemViews(bool failing = false, params ItemRow[] items) : IViewReader
{
    /// <summary>The keys read, in order.</summary>
    public List<string> Reads { get; } = [];

    public ValueTask<Result<TRow?>> Get<TRow>(string view, string key, CancellationToken cancellationToken)
        where TRow : class
    {
        Reads.Add(key);
        if (failing)
        {
            return ValueTask.FromResult<Result<TRow?>>(KernelErrors.NotFound.With("the view is unreadable"));
        }

        var row = view == ViewNames.Items ? items.OfType<TRow>().FirstOrDefault(r => r is ItemRow item && item.Id.Value == key) : null;
        return ValueTask.FromResult<Result<TRow?>>(row);
    }

    public ValueTask<Result<IReadOnlyList<TRow>>> List<TRow>(string view, CancellationToken cancellationToken)
        where TRow : class =>
        throw new NotSupportedException("the list service reads items by key");

    public ValueTask<Result<IReadOnlyList<TRow>>> Range<TRow>(string view, string afterKey, string? beforeKey, int max, CancellationToken cancellationToken)
        where TRow : class =>
        throw new NotSupportedException("the list service reads items by key");

    public ValueTask<Result<IReadOnlyList<TRow>>> Where<TRow>(string view, ViewColumn column, string value, CancellationToken cancellationToken)
        where TRow : class =>
        throw new NotSupportedException("the list service reads items by key");

    public ValueTask<Result<ProjectorState?>> State(CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<ProjectorState?>>((ProjectorState?)null);
}
