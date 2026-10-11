using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Features.GetStatus;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// Bare 'sindri' and 'sindri --headless' (factory addendum §17.29.1): start the control plane if it isn't running
/// (detached, through the launcher), then print the banner and the summary. Exit 1 when the profile needs approval,
/// so a script sees the attention. The start wait runs on the injected clock: up to <see cref="Budget"/>, probing
/// every <see cref="Poll"/>.
/// </summary>
internal sealed class StartCommand(IControlPlaneProbe probe, IControlPlaneLauncher launcher, ControlPlaneClient client, Output output, TimeProvider clock)
{
    /// <summary>How long a start waits for the control plane to answer.</summary>
    internal static readonly TimeSpan Budget = TimeSpan.FromSeconds(10);

    /// <summary>How often the start wait probes.</summary>
    internal static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(200);

    internal async Task<int> Run(bool headless, CancellationToken ct)
    {
        await output.Out.WriteLineAsync(Banner.Text).ConfigureAwait(false);
        var found = await FindOrLaunch(ct).ConfigureAwait(false);
        if (found.IsError)
        {
            return await output.Error(found.Error).ConfigureAwait(false);
        }

        var status = await client.Get<GetStatusResponse>("/sindri/status", ct).ConfigureAwait(false);
        return status.IsError
            ? await output.Error(status.Error).ConfigureAwait(false)
            : await output.Text(SummaryText.Render(status.Value, found.Value.Endpoint, showUi: !headless), status.Value.Profile.NeedsApproval ? 1 : 0)
                .ConfigureAwait(false);
    }

    /// <summary>
    /// The running control plane, or the one this call starts. It launches only when nothing runs (SND-API-001); one that
    /// answers late (SND-API-002) is reported as it is. A child that exits with SND-LEASE-001 lost the lease to a
    /// concurrent start, so polling goes on until the deadline for the winner to answer.
    /// </summary>
    internal async Task<Result<(ControlEndpoint Endpoint, Ping Ping)>> FindOrLaunch(CancellationToken ct)
    {
        var found = await probe.Check(ct).ConfigureAwait(false);
        return found.IsError && found.Error.Code == SindriErrors.NotRunning.Code ? await Launch(ct).ConfigureAwait(false) : found;
    }

    private async Task<Result<(ControlEndpoint Endpoint, Ping Ping)>> Launch(CancellationToken ct)
    {
        var launched = launcher.Launch();
        if (launched.IsError)
        {
            return launched.Error;
        }

        using var child = launched.Value;
        var deadline = clock.GetUtcNow() + Budget;
        while (clock.GetUtcNow() < deadline)
        {
            await Task.Delay(Poll, clock, ct).ConfigureAwait(false);
            var found = await probe.Check(ct).ConfigureAwait(false);
            if (found.IsSuccess)
            {
                return found;
            }

            if (child.HasExited && !child.LastError().StartsWith(SindriErrors.LeaseHeld.Code, StringComparison.Ordinal))
            {
                return SindriErrors.StartFailed.With($"'sindri serve' exited: {child.LastError()} (log: {child.LogPath})");
            }
        }

        var exited = child.HasExited ? $": the started one exited with {child.LastError()}" : string.Empty;   // a lost race names the holder that never answered
        return SindriErrors.StartFailed.With(string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"'sindri serve' didn't answer within {Budget.TotalSeconds:0} s{exited} (log: {child.LogPath})"));
    }
}
