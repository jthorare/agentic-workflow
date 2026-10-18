using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.SessionExit;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Testing;

/// <summary>Builds a SessionExitRequest with deterministic defaults: the first launch with <see cref="FakeLaunchFiles.DefaultProof"/>, exit code 0 and no session id.</summary>
public sealed class SessionExitRequestBuilder
{
    private LaunchId _launchId = A.FirstLaunchId;
    private LaunchSecret _proof = new(FakeLaunchFiles.DefaultProof);
    private int _exitCode;
    private SessionId? _sessionId;

    /// <summary>Sets LaunchId.</summary>
    /// <param name="value">The id.</param>
    /// <returns>This builder.</returns>
    public SessionExitRequestBuilder WithLaunchId(LaunchId value)
    {
        _launchId = value;
        return this;
    }

    /// <summary>Sets the launch's proof.</summary>
    /// <param name="value">The proof the call carries.</param>
    /// <returns>This builder.</returns>
    public SessionExitRequestBuilder WithProof(string value)
    {
        _proof = new LaunchSecret(value);
        return this;
    }

    /// <summary>Sets the exit code.</summary>
    /// <param name="value">The code.</param>
    /// <returns>This builder.</returns>
    public SessionExitRequestBuilder WithExitCode(int value)
    {
        _exitCode = value;
        return this;
    }

    /// <summary>Sets the reported session id.</summary>
    /// <param name="value">The id.</param>
    /// <returns>This builder.</returns>
    public SessionExitRequestBuilder WithSessionId(string value)
    {
        _sessionId = SessionId.From(value);
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public SessionExitRequest Build() => new(_launchId, _proof, _exitCode, _sessionId);
}
