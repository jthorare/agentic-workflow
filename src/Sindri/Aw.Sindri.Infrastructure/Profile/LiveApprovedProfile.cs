using Aw.Kernel;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>The profile runtime reads once approval is gone (#2258 PR 1): the live file, validated on each read. Red stub; the fix lands in the next commit.</summary>
/// <param name="source">The live profile.</param>
internal sealed class LiveApprovedProfile(IProfileSource source) : IApprovedProfile
{
    internal IProfileSource Source { get; } = source;

    public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) => throw new NotSupportedException();
}
