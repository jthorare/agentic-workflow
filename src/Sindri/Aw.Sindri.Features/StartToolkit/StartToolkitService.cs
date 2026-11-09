using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.StartToolkit;

/// <summary>Starts a toolkit that is built for its current source (see <see cref="StartToolkitRequest"/>).</summary>
/// <param name="store">The toolkit store.</param>
/// <param name="workspace">The toolkit folders.</param>
/// <param name="runtime">Starts the process.</param>
internal sealed class StartToolkitService(IToolkitStore store, IToolkitWorkspace workspace, IToolkitRuntime runtime) : IService<StartToolkitRequest, StartToolkitResponse>
{
    public ValueTask<Result<StartToolkitResponse>> Handle(StartToolkitRequest request, CancellationToken cancellationToken)
    {
        _ = (store, workspace, runtime);   // the red stub
        throw new NotSupportedException();
    }
}
