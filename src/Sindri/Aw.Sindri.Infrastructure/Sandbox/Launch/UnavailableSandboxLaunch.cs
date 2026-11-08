using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>The sandbox step Linux and macOS replace: everywhere else every Step launch and every attended open is refused with SND-LAUNCH-014 unsupported-platform, never run bare (sandbox spec SB1, #2016).</summary>
internal sealed class UnavailableSandboxLaunch : ISandboxLaunch
{
    /// <inheritdoc />
    public ValueTask<Result<PreparedSandbox>> Prepare(LaunchRequest request, IReadOnlyList<string> argv, SessionId? chosenSession, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<PreparedSandbox>>(
            SandboxRefusal.For(SindriErrors.LaunchSandboxUnavailable, SandboxReasons.UnsupportedPlatform, "no sandbox for this operating system"));

    /// <inheritdoc />
    public ValueTask Release(LaunchId launch, CancellationToken cancellationToken) => ValueTask.CompletedTask;   // Prepare never prepares anything, so there is nothing to release
}
