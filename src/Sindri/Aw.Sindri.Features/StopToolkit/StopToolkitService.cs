using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.StopToolkit;

/// <summary>Stops a toolkit (see <see cref="StopToolkitRequest"/>).</summary>
/// <param name="store">The toolkit store.</param>
/// <param name="runtime">Stops the process.</param>
internal sealed class StopToolkitService(IToolkitStore store, IToolkitRuntime runtime) : IService<StopToolkitRequest, StopToolkitResponse>
{
    public ValueTask<Result<StopToolkitResponse>> Handle(StopToolkitRequest request, CancellationToken cancellationToken)
    {
        _ = (store, runtime);   // the red stub
        throw new NotSupportedException();
    }
}
