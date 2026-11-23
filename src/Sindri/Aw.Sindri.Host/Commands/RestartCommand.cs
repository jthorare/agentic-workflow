using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Features.GetOpenRuns;
using Aw.Sindri.Features.RestartProcess;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri restart [--wait] [--timeout D]' (first-run spec decision 11, §5.1): POST /sindri/restart, then the caller's half. It takes the old serve's pid from the
/// 202 body, waits up to 30 s for that pid to exit and the port to stop answering, and only then starts serve (this binary, the build the wrapper names). With runs
/// open it exits 1 (SND-SETUP-009) naming each; with --wait it fixes the open set, reads GET /sindri/runs/open every 5 s until that set has ended, and asks again;
/// a run that started meanwhile is named and waited for too, until --timeout. Exit 0 once the new serve answers; 1 runs open, a timeout, or serve didn't come back
/// (SND-SETUP-012); 2 refused (SND-AUTH-002, SND-SETUP-011) or a bad flag.
/// </summary>
/// <param name="client">The control plane's client.</param>
/// <param name="probe">Whether a control plane answers.</param>
/// <param name="processes">Whether the old pid still lives.</param>
/// <param name="start">Starts serve after the stop.</param>
/// <param name="output">Where lines go.</param>
/// <param name="clock">The waits' clock.</param>
internal sealed class RestartCommand(ControlPlaneClient client, IControlPlaneProbe probe, IProcessProbe processes, StartCommand start, Output output, TimeProvider clock)
{
    /// <summary>How often --wait reads the open set, and how often the stop wait probes the old pid.</summary>
    internal static readonly TimeSpan Poll = TimeSpan.FromSeconds(5);

    /// <summary>How long the old serve may take to exit before the restart gives up without starting a second one.</summary>
    internal static readonly TimeSpan StopBudget = TimeSpan.FromSeconds(30);

    internal async Task<int> Run(IReadOnlyList<string> args, CancellationToken ct)
    {
        var parsed = RestartArguments.Parse(args);
        if (parsed.IsError)
        {
            return await output.Error(parsed.Error).ConfigureAwait(false);
        }

        var deadline = clock.GetUtcNow() + parsed.Value.Timeout;
        var named = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            var restarted = await client.Post<RestartProcessRequest, RestartProcessResponse>("/sindri/restart", body: null, ct).ConfigureAwait(false);
            if (restarted.IsSuccess)
            {
                return await Restart(restarted.Value.Value.Pid, ct).ConfigureAwait(false);
            }

            if (!string.Equals(restarted.Error.Code, SindriErrors.SetupRestartRunsOpen.Code, StringComparison.Ordinal) || !parsed.Value.Wait)
            {
                return await Fail(await WithStartHint(restarted.Error, ct).ConfigureAwait(false)).ConfigureAwait(false);
            }

            var waited = await WaitForOpenSet(named, deadline, ct).ConfigureAwait(false);
            if (waited is not null)
            {
                return await Fail(waited).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Fixes the open set now, names each run not named before, and waits until none of that set is open; null once it has ended.</summary>
    private async Task<Error?> WaitForOpenSet(HashSet<string> named, DateTimeOffset deadline, CancellationToken ct)
    {
        var open = await client.Get<GetOpenRunsResponse>("/sindri/runs/open", ct).ConfigureAwait(false);
        if (open.IsError)
        {
            return open.Error;
        }

        var waitingOn = open.Value.Value.Runs.Select(static r => r.Id).ToHashSet(StringComparer.Ordinal);
        var label = named.Count == 0 ? "waiting for: " : "a new run started: ";
        foreach (var run in open.Value.Value.Runs.Where(r => named.Add(r.Id)))
        {
            await output.Out.WriteLineAsync(label + run.Name).ConfigureAwait(false);
        }

        while (waitingOn.Count > 0)
        {
            if (clock.GetUtcNow() >= deadline)
            {
                return SindriErrors.SetupRestartRunsOpen.With($"still open after --timeout: {string.Join(", ", open.Value.Value.Runs.Where(r => waitingOn.Contains(r.Id)).Select(static r => r.Name))}");
            }

            await Task.Delay(Poll, clock, ct).ConfigureAwait(false);
            open = await client.Get<GetOpenRunsResponse>("/sindri/runs/open", ct).ConfigureAwait(false);
            if (open.IsError)
            {
                return open.Error;
            }

            waitingOn.IntersectWith(open.Value.Value.Runs.Select(static r => r.Id));
        }

        return null;
    }

    /// <summary>The caller's half: wait for the old pid and the port, then start serve and wait for it to answer.</summary>
    private async Task<int> Restart(int oldPid, CancellationToken ct)
    {
        var until = clock.GetUtcNow() + StopBudget;
        while ((oldPid > 0 && processes.Facts(oldPid).Running) || await OldServeAnswers(oldPid, ct).ConfigureAwait(false))
        {
            if (clock.GetUtcNow() >= until)
            {
                return await Fail(SindriErrors.SetupServeDidNotStart.With(string.Create(CultureInfo.InvariantCulture, $"Sindri is still stopping (pid {oldPid} is alive after 30 s); nothing was started (fix: sindri once it has exited)"))).ConfigureAwait(false);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), clock, ct).ConfigureAwait(false);
        }

        var started = await start.FindOrLaunch(ct).ConfigureAwait(false);
        return started.IsError
            ? await Fail(SindriErrors.SetupServeDidNotStart.With($"Sindri didn't start: {started.Error.Code} {started.Error.Message} (fix: sindri serve)")).ConfigureAwait(false)
            : await output.Text(string.Create(CultureInfo.InvariantCulture, $"restarted: pid {oldPid} stopped, pid {started.Value.Endpoint.Pid} serves {started.Value.Endpoint.Url}\n"), 0).ConfigureAwait(false);
    }

    /// <summary>Whether the old serve still answers the port; a serve with another pid (the app's own start won the race) is the new one, found by FindOrLaunch.</summary>
    private async Task<bool> OldServeAnswers(int oldPid, CancellationToken ct) =>
        await probe.Check(ct).ConfigureAwait(false) is { IsSuccess: true } answer && answer.Value.Endpoint.Pid == oldPid;

    /// <summary>
    /// A restart whose stop failed halfway leaves no serve: when nothing answers, the error says how to start one. A refusal and an error that names its own fix are kept.
    /// </summary>
    private async Task<Error> WithStartHint(Error error, CancellationToken ct) =>
        error.Message.Contains("(fix:", StringComparison.Ordinal) || (await probe.Check(ct).ConfigureAwait(false)).IsSuccess
            ? error
            : error with { Message = error.Message + " (fix: sindri)" };

    /// <summary>Prints the error: exit 1 for runs open, a timeout or serve that didn't come back (and 'not running'), 2 for a refusal.</summary>
    private async Task<int> Fail(Error error)
    {
        var printed = await output.Error(error).ConfigureAwait(false);
        return string.Equals(error.Code, SindriErrors.SetupRestartRunsOpen.Code, StringComparison.Ordinal) || string.Equals(error.Code, SindriErrors.SetupServeDidNotStart.Code, StringComparison.Ordinal)
            ? 1
            : printed;
    }
}
