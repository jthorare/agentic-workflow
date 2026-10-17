using Aw.Sindri.Features.ProbeLaunchTargets;

namespace Aw.Sindri.Testing;

/// <summary>Builds a ProbeLaunchTargetsRequest with deterministic defaults.</summary>
public sealed class ProbeLaunchTargetsRequestBuilder
{
    private readonly ProbeLaunchTargetsRequest _request = new();

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public ProbeLaunchTargetsRequest Build() => _request;
}
