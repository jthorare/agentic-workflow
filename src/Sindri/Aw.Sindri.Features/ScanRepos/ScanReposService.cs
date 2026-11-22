using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ScanRepos;

/// <summary>A stub until the scan commit.</summary>
internal sealed class ScanReposService : IService<ScanReposRequest, ScanReposResponse>
{
    public ValueTask<Result<ScanReposResponse>> Handle(ScanReposRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("ScanRepos is not built yet (first-run spec §3.3)");
}
