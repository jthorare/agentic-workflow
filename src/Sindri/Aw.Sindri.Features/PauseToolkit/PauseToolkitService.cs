using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.PauseToolkit;

/// <summary>Pauses a toolkit (see <see cref="PauseToolkitRequest"/>).</summary>
/// <param name="store">The toolkit store.</param>
/// <param name="runtime">Owns the process.</param>
internal sealed class PauseToolkitService(IToolkitStore store, IToolkitRuntime runtime) : IService<PauseToolkitRequest, PauseToolkitResponse>
{
    public ValueTask<Result<PauseToolkitResponse>> Handle(PauseToolkitRequest request, CancellationToken cancellationToken)
    {
        _ = (store, runtime);
        throw new NotSupportedException();
    }
}
