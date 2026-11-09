using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.ListToolkits;

/// <summary>Reads the toolkit store.</summary>
/// <param name="store">The toolkit store.</param>
internal sealed class ListToolkitsService(IToolkitStore store) : IService<ListToolkitsRequest, ListToolkitsResponse>
{
    public async ValueTask<Result<ListToolkitsResponse>> Handle(ListToolkitsRequest request, CancellationToken cancellationToken)
    {
        var all = await store.List(cancellationToken).ConfigureAwait(false);
        return all.IsError ? all.Error : new ListToolkitsResponse(all.Value);
    }
}
