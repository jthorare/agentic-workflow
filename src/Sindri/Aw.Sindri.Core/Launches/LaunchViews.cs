using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launches;

/// <summary>The 'launches' view read through the views reader (UI spec E-1): one launch by id, and an item's or a repo's launches, newest request first.</summary>
/// <param name="reader">The views read side.</param>
internal sealed class LaunchViews(IViewReader reader) : ILaunchViews
{
    public ValueTask<Result<LaunchRow?>> Get(LaunchId launch, CancellationToken cancellationToken) =>
        reader.Get<LaunchRow>(ViewNames.Launches, launch.Value, cancellationToken);

    public async ValueTask<Result<IReadOnlyList<LaunchRow>>> ForItem(ItemId item, CancellationToken cancellationToken) =>
        Newest(await reader.Where<LaunchRow>(ViewNames.Launches, ViewColumn.Item, item.Value, cancellationToken).ConfigureAwait(false));

    public async ValueTask<Result<IReadOnlyList<LaunchRow>>> ForRepo(RepoName repo, CancellationToken cancellationToken) =>
        Newest(await reader.Where<LaunchRow>(ViewNames.Launches, ViewColumn.Repo, repo.Value, cancellationToken).ConfigureAwait(false));

    private static Result<IReadOnlyList<LaunchRow>> Newest(Result<IReadOnlyList<LaunchRow>> rows) =>
        rows.IsError ? rows.Error : Results.Success<IReadOnlyList<LaunchRow>>([.. rows.Value.OrderByDescending(static r => r.RequestedAt).ThenByDescending(static r => r.Sequence)]);
}
