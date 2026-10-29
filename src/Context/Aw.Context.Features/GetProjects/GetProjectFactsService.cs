using Aw.Context.Core.Projects;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sync;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetProjects;

/// <summary>The stored projects with their open-issue counts, the owner and where the feeds stand (projects page spec §6.1).</summary>
internal sealed class GetProjectFactsService : IService<GetProjectFactsRequest, ProjectFactsAnswer>
{
    public GetProjectFactsService(IRecordStore store, ISourceIdentityStore identities, IIndexSync sync)
    {
        _ = (store, identities, sync);
    }

    public ValueTask<Result<ProjectFactsAnswer>> Handle(GetProjectFactsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
