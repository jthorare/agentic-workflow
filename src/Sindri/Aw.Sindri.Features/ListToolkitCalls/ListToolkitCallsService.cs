using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.ListToolkitCalls;

/// <summary>Lists toolkit calls, newest first. A named toolkit must exist (SND-TK-009 otherwise), so a typo never reads as "no calls yet".</summary>
/// <param name="store">The toolkit store, read to check a named toolkit exists.</param>
/// <param name="calls">The call store.</param>
internal sealed class ListToolkitCallsService(IToolkitStore store, IToolkitCalls calls) : IService<ListToolkitCallsRequest, ListToolkitCallsResponse>
{
    private readonly IToolkitStore _store = store;
    private readonly IToolkitCalls _calls = calls;

    internal const int MaxLimit = 500;

    public async ValueTask<Result<ListToolkitCallsResponse>> Handle(ListToolkitCallsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Toolkit is { } name)
        {
            var found = await _store.Find(name, cancellationToken).ConfigureAwait(false);
            if (found.IsError)
            {
                return found.Error;
            }

            if (found.Value is null)
            {
                return SindriErrors.ToolkitNotFound.With($"'{name}' (fix: sindri toolkit list)");
            }
        }

        var listed = await _calls.List(request.Toolkit, Math.Clamp(request.Limit, 1, MaxLimit), cancellationToken).ConfigureAwait(false);
        return listed.IsError ? listed.Error : new ListToolkitCallsResponse(listed.Value);
    }
}
