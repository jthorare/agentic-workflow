using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Asks the launcher for a session and waits out a full session cap (SND-LAUNCH-020, a queue condition and not a fault; sandbox spec §6): it asks again every
/// <see cref="AgentRuntimeOptions.SlotRetry"/> until <see cref="AgentRuntimeOptions.SlotSlice"/> ends, recording only that it waits (no outcome).
/// </summary>
/// <param name="launcher">The launcher.</param>
/// <param name="options">How long to wait for a free session slot.</param>
/// <param name="recorder">Records the first wait, so the status can say the Step waits for a slot.</param>
internal sealed class AgentLaunchWaiting(ILauncher launcher, AgentRuntimeOptions options, AgentLaunchRecorder recorder)
{
    /// <summary>Launches, asking again while the cap is full.</summary>
    /// <param name="request">The launch.</param>
    /// <param name="cancellationToken">Cancels the launch and the wait.</param>
    /// <returns>The launcher's answer; null when the slice ended with the cap still reached.</returns>
    public async Task<LaunchResult?> Launch(LaunchRequest request, CancellationToken cancellationToken)
    {
        for (var waited = TimeSpan.Zero; ; waited += options.SlotRetry)
        {
            var result = await launcher.Launch(request, cancellationToken).ConfigureAwait(false);
            if (result is not FailedLaunch failed || SandboxCodes.KindOf(failed.Error.Code) != LaunchCodeKind.ResourceWait)
            {
                return result;
            }

            await recorder.Waiting(request, failed, cancellationToken).ConfigureAwait(false);   // idempotent: the first wait is the one recorded
            if (waited + options.SlotRetry > options.SlotSlice)
            {
                return null;
            }

            await Task.Delay(options.SlotRetry, cancellationToken).ConfigureAwait(false);
        }
    }
}
