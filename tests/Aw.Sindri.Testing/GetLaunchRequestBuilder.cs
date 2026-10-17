using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.GetLaunch;

namespace Aw.Sindri.Testing;

/// <summary>Builds a GetLaunchRequest with deterministic defaults.</summary>
public sealed class GetLaunchRequestBuilder
{
    private LaunchId _launchId = A.FirstLaunchId;

    /// <summary>Sets LaunchId.</summary>
    /// <param name="value">The id.</param>
    /// <returns>This builder.</returns>
    public GetLaunchRequestBuilder WithLaunchId(LaunchId value)
    {
        _launchId = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public GetLaunchRequest Build() => new(_launchId);
}
