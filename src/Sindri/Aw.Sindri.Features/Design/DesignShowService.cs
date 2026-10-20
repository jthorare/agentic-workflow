using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Design;

/// <summary>'design show' and 'plan show --facts': the stop-2 view over a directory of packet files.</summary>
internal sealed class DesignShowService : IService<DesignShowRequest, DesignStopView>
{
    public ValueTask<Result<DesignStopView>> Handle(DesignShowRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult<Result<DesignStopView>>(DesignStopViewBuilder.Build(new LineFacts(request.Facts)));
    }
}
