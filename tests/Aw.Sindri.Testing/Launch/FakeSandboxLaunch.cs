using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Testing.Launch;

/// <summary>An <see cref="ISandboxLaunch"/> that wraps the argv in 'fake-sandbox --' (or refuses with the error it is given) and records what it was asked.</summary>
public sealed class FakeSandboxLaunch : ISandboxLaunch
{
    private readonly List<LaunchId> _released = [];
    private readonly List<(LaunchRequest Request, IReadOnlyList<string> Argv)> _prepared = [];

    /// <summary>The refusal Prepare returns, or null to confine.</summary>
    public Error? Refuse { get; set; }

    /// <summary>Makes Release throw, to prove a failed release never fails a stop.</summary>
    public bool ReleaseThrows { get; set; }

    /// <summary>The session home Prepare hands back.</summary>
    public string SessionHome { get; set; } = "/sessions/home";

    /// <summary>The summary Prepare hands back.</summary>
    public SandboxSummary Summary { get; } = new(SandboxMechanism.Bubblewrap, "9f2c0a41d8e6", ["/work"], ["model"], NetworkMode.Enforce, InnerState.On,
        new SandboxLimitsSummary("12GiB", "400%", 2048), new SandboxCredentialsSummary(GithubCredentials.None), ModelLoginSource.OwnerCopy);

    /// <summary>The summary Prepare hands back for an attended launch (sandbox spec §3.9).</summary>
    public SandboxSummary AttendedSummary => Summary with { Attended = true };

    /// <summary>The launches Prepare was called for, with the argv it got.</summary>
    public IReadOnlyList<(LaunchRequest Request, IReadOnlyList<string> Argv)> Prepared => _prepared;

    /// <summary>The launches Release was called for.</summary>
    public IReadOnlyList<LaunchId> Released => _released;

    /// <inheritdoc/>
    public ValueTask<Result<PreparedSandbox>> Prepare(LaunchRequest request, IReadOnlyList<string> argv, SessionId? chosenSession, CancellationToken cancellationToken)
    {
        _prepared.Add((request, argv));
        return ValueTask.FromResult(Refuse is { } error
            ? (Result<PreparedSandbox>)error
            : new PreparedSandbox(request.Attended ? AttendedSummary : Summary, ["fake-sandbox", "--", .. argv], SessionHome, "/run/area"));
    }

    /// <inheritdoc/>
    public ValueTask Release(LaunchId launch, CancellationToken cancellationToken)
    {
        _released.Add(launch);
        return ReleaseThrows ? throw new InvalidOperationException("release failed") : ValueTask.CompletedTask;
    }
}
