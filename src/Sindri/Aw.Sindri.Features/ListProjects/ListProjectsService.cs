using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListProjects;

/// <summary>The Projects page (projects page spec §6.2): the owner state from views.db, Context's facts, merged by ProjectCards.</summary>
/// <param name="pages">Reads the views and the facts and builds the page.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller; a read needs one.</param>
internal sealed class ListProjectsService(IProjectPages pages, IProjectorStatus status, ICallerContextAccessor callers) : IService<ListProjectsRequest, ListProjectsResponse>
{
    public async ValueTask<Result<ListProjectsResponse>> Handle(ListProjectsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("the Projects page needs a caller");
        }

        var read = await pages.Read(request.All, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var page = read.Value;
        return new ListProjectsResponse(status.Current.AsOf, page.Empty, page.Viewer, page.Freshness, page.Projects, page.Finished, page.Hidden);
    }
}
