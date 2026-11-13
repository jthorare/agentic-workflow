using System.Globalization;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.InitProfile;
using Aw.Sindri.Features.RebuildViews;
using Aw.Sindri.Features.RestartServe;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Features.SetProfileKey;
using Aw.Sindri.Features.StopProcess;
using Aw.Sindri.Features.ValidateProfile;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The client verbs. status, stop, profile approve and views rebuild call the running control plane's API; profile validate and doctor
/// run the same handlers in this process, read-only, so they work while it is down.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="probe">Finds the running control plane.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="scope">The scope the local verbs resolve their handlers from.</param>
/// <param name="ensureRunning">Starts the control plane before a human's command, or names why not (API spec §7); null: never start it.</param>
internal sealed class Verbs(ControlPlaneClient client, Core.Control.IControlPlaneProbe probe, Output output, IServiceProvider scope,
    Func<CancellationToken, Task<Kernel.Error?>>? ensureRunning = null)
{
    internal async Task<int> Status(bool json, CancellationToken ct)
    {
        var found = await probe.Check(ct).ConfigureAwait(false);
        var status = found.IsError ? found.Error : await client.Get<GetStatusResponse>("/sindri/status", ct).ConfigureAwait(false);
        if (status.IsError)
        {
            var error = status.Error.Code == SindriErrors.NotRunning.Code ? status.Error with { Message = "not running: " + status.Error.Message } : status.Error;
            return await output.Error(error).ConfigureAwait(false);
        }

        var code = StatusExitCode(status.Value.Value);
        return json
            ? await output.Raw(status.Value.Body, code).ConfigureAwait(false)
            : await output.Text(SummaryText.Render(status.Value.Value, found.Value.Endpoint, ui: null), code).ConfigureAwait(false);
    }

    /// <summary>The 'sindri status' exit code: 1 unless the health is healthy (a server without health: 0).</summary>
    internal static int StatusExitCode(GetStatusResponse status) =>
        status.Health is { } health ? (health.Word == HealthWord.Healthy ? 0 : 1) : 0;

    internal async Task<int> Stop(CancellationToken ct)
    {
        var stopped = await client.Post<StopProcessRequest, StopProcessResponse>("/sindri/stop", body: null, ct).ConfigureAwait(false);
        return stopped.IsError
            ? await output.Error(stopped.Error).ConfigureAwait(false)
            : await output.Text(string.Create(CultureInfo.InvariantCulture, $"stopped (pid {stopped.Value.Value.Pid}, epoch {stopped.Value.Value.Epoch} released)\n"), 0).ConfigureAwait(false);
    }

    /// <summary>'sindri restart' (#3052): stops serve and lets the relauncher start the installed build; prints the runs it leaves open.</summary>
    internal async Task<int> Restart(CancellationToken ct)
    {
        var restarted = await client.Post<RestartServeRequest, RestartServeResponse>("/sindri/restart", body: null, ct).ConfigureAwait(false);
        if (restarted.IsError)
        {
            return await output.Error(restarted.Error).ConfigureAwait(false);
        }

        var r = restarted.Value.Value;
        var kept = r.KeptRuns.Count == 0 ? "no open agent runs" : string.Join("; ", r.KeptRuns.Select(static k => $"{k.Ref} on {k.Instance}"));
        return await output.Text(string.Create(CultureInfo.InvariantCulture, $"restarting (pid {r.Pid}): {r.FromBuild} -> {r.ToBuild}; keeping {kept}\n"), 0).ConfigureAwait(false);
    }

    /// <summary>'sindri views rebuild': starts the control plane if this CLI may (API spec §7), then posts RebuildViews and prints the sequence it recorded.</summary>
    internal async Task<int> RebuildViews(CancellationToken ct)
    {
        var down = ensureRunning is null ? null : await ensureRunning(ct).ConfigureAwait(false);
        if (down is not null)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        var accepted = await client.Post<RebuildViewsRequest, CommandAccepted>("/sindri/views/rebuild", body: null, ct).ConfigureAwait(false);
        return accepted.IsError
            ? await output.Error(accepted.Error).ConfigureAwait(false)
            : await output.Text(string.Create(CultureInfo.InvariantCulture, $"views rebuild requested (sequence {accepted.Value.Value.Sequence})\n"), 0).ConfigureAwait(false);
    }

    internal async Task<int> Validate(bool json, CancellationToken ct)
    {
        var valid = await scope.GetRequiredService<IService<ValidateProfileRequest, ValidateProfileResponse>>().Handle(new ValidateProfileRequest(), ct).ConfigureAwait(false);
        if (valid.IsError)
        {
            return await output.Error(valid.Error).ConfigureAwait(false);
        }

        return json
            ? await output.Json(valid.Value, 0).ConfigureAwait(false)
            : await output.Text($"Profile valid: {valid.Value.Source}\nhash {valid.Value.Hash.Value}\n{string.Concat((valid.Value.Matches ?? []).Select(static l => l + "\n"))}{string.Concat((valid.Value.Warnings ?? []).Select(static l => "warning: " + l + "\n"))}", 0).ConfigureAwait(false);
    }

    /// <summary>'profile init [--json]': writes the default profile when none exists and says what comes next; local, like validate. It never replaces a file.</summary>
    internal async Task<int> InitProfile(bool json, CancellationToken ct)
    {
        var written = await scope.GetRequiredService<IService<InitProfileRequest, InitProfileResponse>>().Handle(new InitProfileRequest(), ct).ConfigureAwait(false);
        if (written.IsError)
        {
            return await output.Error(written.Error).ConfigureAwait(false);
        }

        return json
            ? await output.Json(written.Value, 0).ConfigureAwait(false)
            : await output.Text($"Profile written: {written.Value.Path}\nnext: sindri repo add <path>\n", 0).ConfigureAwait(false);
    }

    /// <summary>
    /// 'profile set &lt;key&gt; &lt;value&gt; [--json]': sets one scalar key of the live profile in place; the edit is live at once; local, like init. A CLI with no readable per-start human token, or a session Sindri launched, is refused (SND-AUTH-002, like 'sandbox login'). 
    /// Exit 0 set or unchanged, 1 an invalid key or value or a layout it won't edit (nothing written), 2 refused or the file can't be read or written.
    /// </summary>
    internal async Task<int> SetProfileKey(string key, string value, bool json, CallerHeaders caller, CancellationToken ct)
    {
        if (await caller.Resolve(ct).ConfigureAwait(false) is { } reason)
        {
            return await output.Error(SindriErrors.AuthRefused.With($"profile set is for the person at the keyboard, and this is {reason} (fix: run it from your own terminal)")).ConfigureAwait(false);
        }

        var set = await scope.GetRequiredService<IService<SetProfileKeyRequest, SetProfileKeyResponse>>().Handle(new SetProfileKeyRequest(key, value), ct).ConfigureAwait(false);
        if (set.IsError)
        {
            var code = await output.Error(set.Error).ConfigureAwait(false);
            return string.Equals(set.Error.Code, SindriErrors.ProfileMissing.Code, StringComparison.Ordinal) ? code : 1;
        }

        var done = set.Value;
        return json
            ? await output.Json(done, 0).ConfigureAwait(false)
            : await output.Text(done.Changed
                ? $"set {done.Key} = {done.Value} in {done.Path}\n"
                : $"{done.Key} is already {done.Value} in {done.Path}: unchanged\n", 0).ConfigureAwait(false);
    }

    internal async Task<int> Doctor(bool json, CancellationToken ct, Func<CancellationToken, ValueTask<IReadOnlyList<DoctorCheck>>>? harnesses = null)
    {
        var request = new RunDoctorRequest
        {
            OnRow = json ? null : row => { output.Out.Write(Line(row)); output.Out.Flush(); },   // text: each row prints as its check finishes (#3390)
            Harnesses = harnesses,   // the pinned harness probe's lines (engine spec §6.2), in the same parallel pass (#3390)
        };
        var report = (await scope.GetRequiredService<IService<RunDoctorRequest, RunDoctorResponse>>().Handle(request, ct).ConfigureAwait(false)).Value;
        return json
            ? await output.Json(report, report.ExitCode).ConfigureAwait(false)
            : report.ExitCode;
    }

    private static string Line(DoctorCheck c) => $"{Word(c.State),-5} {c.Name,-17} {c.Detail}{(c.Fix is null ? string.Empty : $" (fix: {c.Fix})")}\n";

    /// <summary>'profile approve [&lt;hash&gt;]' after approval is gone (#2258 PR 1): says so and exits 0, so an old script keeps going; needs no control plane.</summary>
    internal async Task<int> Approve(string? hashText, CancellationToken ct)
    {
        _ = (hashText, ct);
        return await output.Text("removed: edits apply on save (nothing to approve; `sindri profile set`, `repo add` and the app write the live profile)\n", 0).ConfigureAwait(false);
    }

    private static string Word(CheckState state) => state switch
    {
        CheckState.Ok => "ok",
        CheckState.Warn => "warn",
        CheckState.Fail => "fail",
        _ => "?",
    };
}
