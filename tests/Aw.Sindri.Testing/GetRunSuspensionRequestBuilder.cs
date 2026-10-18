using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.GetRunSuspension;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Testing;

/// <summary>Builds a GetRunSuspensionRequest with deterministic defaults: the first launch with <see cref="FakeLaunchFiles.DefaultProof"/>.</summary>
public sealed class GetRunSuspensionRequestBuilder
{
    private LaunchId _launchId = A.FirstLaunchId;
    private LaunchSecret _proof = new(FakeLaunchFiles.DefaultProof);

    /// <summary>Sets LaunchId.</summary>
    /// <param name="value">The id.</param>
    /// <returns>This builder.</returns>
    public GetRunSuspensionRequestBuilder WithLaunchId(LaunchId value)
    {
        _launchId = value;
        return this;
    }

    /// <summary>Sets the launch secret.</summary>
    /// <param name="value">The secret.</param>
    /// <returns>This builder.</returns>
    public GetRunSuspensionRequestBuilder WithProof(string value)
    {
        _proof = new LaunchSecret(value);
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public GetRunSuspensionRequest Build() => new(_launchId, _proof);
}
