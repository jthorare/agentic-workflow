using Aw.Kernel;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>The items view, the timeline, decisions and requests views with fixed answers, for the detail's title and counts.</summary>
internal sealed class DetailViews(ItemRow? item = null, int timeline = 0, int decisions = 0, int requests = 0, IReadOnlyList<NeedsYouRow>? needsYou = null, bool needsYouFails = false) : IViewReader
{
    public ValueTask<Result<TRow?>> Get<TRow>(string view, string key, CancellationToken cancellationToken)
        where TRow : class =>
        ValueTask.FromResult(Row<TRow>(view, key));

    public ValueTask<Result<IReadOnlyList<TRow>>> List<TRow>(string view, CancellationToken cancellationToken)
        where TRow : class =>
        throw new NotSupportedException("the detail reads by key, range and column");

    public ValueTask<Result<IReadOnlyList<TRow>>> Range<TRow>(string view, string afterKey, string? beforeKey, int max, CancellationToken cancellationToken)
        where TRow : class =>
        ValueTask.FromResult(Results.Success(Rows<TRow>(timeline)));

    public ValueTask<Result<IReadOnlyList<TRow>>> Where<TRow>(string view, ViewColumn column, string value, CancellationToken cancellationToken)
        where TRow : class =>
        ValueTask.FromResult(Results.Success(Rows<TRow>(view == ViewNames.Decisions ? decisions : requests)));

    public ValueTask<Result<ProjectorState?>> State(CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<ProjectorState?>>((ProjectorState?)null);

    private Result<TRow?> Row<TRow>(string view, string key)
        where TRow : class
    {
        if (view == ViewNames.NeedsYou)
        {
            return needsYouFails
                ? new Error("SND-TEST-001", ErrorKind.Unavailable, "the needs-you view can't be read")
                : Results.Success(needsYou?.FirstOrDefault(r => r.Id.Value == key) is { } row ? new NeedsYouRowRecord(row, 1) as TRow : null);
        }

        return Results.Success(view == ViewNames.Items && item is not null && item.Id.Value == key ? item as TRow : null);
    }

    private static IReadOnlyList<TRow> Rows<TRow>(int count)
        where TRow : class => [.. Enumerable.Repeat<TRow>(null!, count)];   // why: only the number of rows is read
}
