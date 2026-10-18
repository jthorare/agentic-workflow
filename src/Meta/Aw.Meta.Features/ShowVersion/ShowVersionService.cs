using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.ShowVersion;

/// <summary>Reports the commit, build time, OS and runtime identifier of the running binary.</summary>
/// <param name="build">The binary's stamp.</param>
internal sealed class ShowVersionService(IBuildInfo build) : IService<ShowVersionRequest, BuildInfo>
{
    public ValueTask<Result<BuildInfo>> Handle(ShowVersionRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success(build.Read()));
}
