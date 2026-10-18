using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.SessionHeartbeat;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Testing;

/// <summary>Builds a SessionHeartbeatRequest with deterministic defaults: the first launch with <see cref="FakeLaunchFiles.DefaultProof"/> and no session id.</summary>
public sealed class SessionHeartbeatRequestBuilder
{
    private LaunchId _launchId = A.FirstLaunchId;
    private LaunchSecret _proof = new(FakeLaunchFiles.DefaultProof);
    private SessionId? _sessionId;

    /// <summary>Sets LaunchId.</summary>
    /// <param name="value">The id.</param>
    /// <returns>This builder.</returns>
    public SessionHeartbeatRequestBuilder WithLaunchId(LaunchId value)
    {
        _launchId = value;
        return this;
    }

    /// <summary>Sets the launch's proof.</summary>
    /// <param name="value">The proof the call carries.</param>
    /// <returns>This builder.</returns>
    public SessionHeartbeatRequestBuilder WithProof(string value)
    {
        _proof = new LaunchSecret(value);
        return this;
    }

    /// <summary>Sets the reported session id.</summary>
    /// <param name="value">The id.</param>
    /// <returns>This builder.</returns>
    public SessionHeartbeatRequestBuilder WithSessionId(string value)
    {
        _sessionId = SessionId.From(value);
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public SessionHeartbeatRequest Build() => new(_launchId, _proof, _sessionId);
}
