using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.ListToolkitCalls;

/// <summary>Lists toolkit calls, newest first.</summary>
/// <param name="calls">The call store.</param>
internal sealed class ListToolkitCallsService(IToolkitCalls calls) : IService<ListToolkitCallsRequest, ListToolkitCallsResponse>
{
    private readonly IToolkitCalls _calls = calls;

    internal const int MaxLimit = 500;

    public async ValueTask<Result<ListToolkitCallsResponse>> Handle(ListToolkitCallsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var listed = await _calls.List(request.Toolkit, Math.Clamp(request.Limit, 1, MaxLimit), cancellationToken).ConfigureAwait(false);
        return listed.IsError ? listed.Error : new ListToolkitCallsResponse(listed.Value);
    }
}
