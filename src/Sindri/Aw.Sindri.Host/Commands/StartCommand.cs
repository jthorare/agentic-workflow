using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Features.GetStatus;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// Bare 'sindri' and 'sindri --headless' (factory addendum §17.29.1): start the control plane if it isn't running
/// (detached, through the launcher), open the app unless headless (<paramref name="openUi"/> is null), then print the
/// banner and the summary. Exit 1 when the profile needs approval, so a script sees the attention; the app never changes
/// the exit code. The start wait runs on the injected clock: up to <see cref="Budget"/>, probing every <see cref="Poll"/>.
/// </summary>
internal sealed class StartCommand(IControlPlaneProbe probe, IControlPlaneLauncher launcher, ControlPlaneClient client, Output output, TimeProvider clock,
    Func<string>? openUi = null)
{
    /// <summary>Gets a step that runs after the banner and before the control plane is looked for or started: the first-run Onyx offer, for bare 'sindri' at a terminal.</summary>
    internal Func<CancellationToken, Task>? BeforeStart { get; init; }

    /// <summary>How long a start waits for the control plane to answer: a loaded box (CI, a cold disk) can take well over 10 s to bind. A serve that exited ends the wait at once, so a long budget never hides a dead one.</summary>
    internal static readonly TimeSpan Budget = TimeSpan.FromSeconds(120);

    /// <summary>How often the start wait probes.</summary>
    internal static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(200);

    internal async Task<int> Run(CancellationToken ct)
    {
        await output.Out.WriteLineAsync(Banner.Text).ConfigureAwait(false);
        if (BeforeStart is not null)
        {
            await BeforeStart(ct).ConfigureAwait(false);   // the first-run Onyx offer, for bare 'sindri' at a terminal
        }

        var found = await FindOrLaunch(ct).ConfigureAwait(false);
        if (found.IsError)
        {
            return await output.Error(found.Error).ConfigureAwait(false);
        }

        var status = await client.Get<GetStatusResponse>("/sindri/status", ct).ConfigureAwait(false);
        if (status.IsError)
        {
            return await output.Error(status.Error).ConfigureAwait(false);
        }

        return await Report(status.Value.Value, found.Value.Endpoint).ConfigureAwait(false);
    }

    /// <summary>Opens the app (unless headless) and prints the summary; the exit code follows only the profile.</summary>
    internal Task<int> Report(GetStatusResponse status, ControlEndpoint endpoint)
    {
        var ui = openUi?.Invoke();
        return output.Text(SummaryText.Render(status, endpoint, ui), status.Profile.NeedsApproval ? 1 : 0);
    }

    /// <summary>
    /// The running control plane, or the one this call starts. It launches only when nothing runs (SND-API-001), and
    /// one that answers late (SND-API-002) is reported as it is. A child that exits with SND-LEASE-001 lost the lease to a concurrent start, so polling goes
    /// on until the deadline for the winner to answer.
    /// </summary>
    internal Task<Result<(ControlEndpoint Endpoint, Ping Ping)>> FindOrLaunch(CancellationToken ct) => Find(launch: true, ct);

    /// <summary>
    /// Before a command that needs the control plane (API spec §7): null when it runs, or once this call started it;
    /// otherwise the error to print. It starts it only when <paramref name="launch"/> (no --no-start).
    /// </summary>
    /// <param name="launch">False for --no-start.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns>Null, or the error.</returns>
    internal async Task<Error?> EnsureRunning(bool launch, CancellationToken ct)
    {
        var found = await Find(launch, ct).ConfigureAwait(false);
        return found.IsError ? found.Error : null;
    }

    private async Task<Result<(ControlEndpoint Endpoint, Ping Ping)>> Find(bool launch, CancellationToken ct)
    {
        if (client.SettingRefusal is { } refused)
        {
            return refused;   // a malformed setting is refused before a start launches anything
        }

        var found = await probe.Check(ct).ConfigureAwait(false);
        if (found.IsSuccess || found.Error.Code != SindriErrors.NotRunning.Code)
        {
            return found;
        }

        return launch ? await Launch(ct).ConfigureAwait(false) : found;
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
