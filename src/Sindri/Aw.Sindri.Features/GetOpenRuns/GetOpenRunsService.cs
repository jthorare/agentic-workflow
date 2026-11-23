using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.GetOpenRuns;

/// <summary>GET /sindri/runs/open (first-run spec decision 11): the open set a restart would wait for, each with its page link.</summary>
/// <param name="open">The open-set read.</param>
internal sealed class GetOpenRunsService(IOpenRuns open) : IService<GetOpenRunsRequest, GetOpenRunsResponse>
{
    public async ValueTask<Result<GetOpenRunsResponse>> Handle(GetOpenRunsRequest request, CancellationToken cancellationToken)
    {
        var read = await open.Read(cancellationToken).ConfigureAwait(false);
        return read.IsError ? read.Error : new GetOpenRunsResponse([.. read.Value.Select(static r => new OpenRunLine(r.Kind, r.Ref, r.Name, r.Link))]);
    }
}
