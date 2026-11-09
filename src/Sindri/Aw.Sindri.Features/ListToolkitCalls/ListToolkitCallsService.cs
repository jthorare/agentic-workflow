using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.ListToolkitCalls;

/// <summary>Lists toolkit calls, newest first.</summary>
/// <param name="calls">The call store.</param>
internal sealed class ListToolkitCallsService(IToolkitCalls calls) : IService<ListToolkitCallsRequest, ListToolkitCallsResponse>
{
    private readonly IToolkitCalls _calls = calls;

    public ValueTask<Result<ListToolkitCallsResponse>> Handle(ListToolkitCallsRequest request, CancellationToken cancellationToken)
    {
        _ = _calls;
        throw new NotSupportedException();
    }
}
