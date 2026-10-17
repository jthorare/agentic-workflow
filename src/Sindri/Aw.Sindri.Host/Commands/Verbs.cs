using System.Globalization;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.IssueChallenge;
using Aw.Sindri.Features.OnyxStatus;
using Aw.Sindri.Features.RebuildViews;
using Aw.Sindri.Features.RunDoctor;
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

    /// <summary>The 'sindri status' exit code: 1 unless the health is healthy (a server without health: unless the profile is approved).</summary>
    internal static int StatusExitCode(GetStatusResponse status) =>
        status.Health is { } health ? (health.Word == HealthWord.Healthy ? 0 : 1) : (status.Profile.NeedsApproval ? 1 : 0);

    internal async Task<int> Stop(CancellationToken ct)
    {
        var stopped = await client.Post<StopProcessRequest, StopProcessResponse>("/sindri/stop", body: null, ct).ConfigureAwait(false);
        return stopped.IsError
            ? await output.Error(stopped.Error).ConfigureAwait(false)
            : await output.Text(string.Create(CultureInfo.InvariantCulture, $"stopped (pid {stopped.Value.Value.Pid}, epoch {stopped.Value.Value.Epoch} released)\n"), 0).ConfigureAwait(false);
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

        var state = valid.Value.Approved ? "approved" : "not approved (fix: sindri profile approve)";
        return json
            ? await output.Json(valid.Value, valid.Value.Approved ? 0 : 1).ConfigureAwait(false)
            : await output.Text($"Profile valid: {valid.Value.Source}\nhash {valid.Value.Hash.Value}, {state}\n", valid.Value.Approved ? 0 : 1).ConfigureAwait(false);
    }

    internal async Task<int> Doctor(bool json, CancellationToken ct, Func<CancellationToken, ValueTask<IReadOnlyList<DoctorCheck>>>? harnesses = null)
    {
        var report = (await scope.GetRequiredService<IService<RunDoctorRequest, RunDoctorResponse>>().Handle(new RunDoctorRequest(), ct).ConfigureAwait(false)).Value;
        if (harnesses is not null)   // the pinned harness probe's lines (engine spec §6.2)
        {
            DoctorCheck[] checks = [.. report.Checks, .. await harnesses(ct).ConfigureAwait(false)];
            report = new RunDoctorResponse(checks, DoctorRules.ExitCode(checks));
        }

        if (json)
        {
            return await output.Json(report, report.ExitCode).ConfigureAwait(false);
        }

        var lines = report.Checks.Select(c => $"{Word(c.State),-5} {c.Name,-17} {c.Detail}{(c.Fix is null ? string.Empty : $" (fix: {c.Fix})")}");
        return await output.Text(string.Join('\n', lines) + "\n", report.ExitCode).ConfigureAwait(false);
    }

    internal async Task<int> OnyxStatus(bool json, CancellationToken ct)
    {
        var answer = await scope.GetRequiredService<IService<OnyxStatusRequest, OnyxStatusResponse>>().Handle(new OnyxStatusRequest(), ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await OnyxVerbs.Fail(output, answer.Error).ConfigureAwait(false);
        }

        var status = answer.Value;
        if (json)
        {
            return await output.Json(status, status.ExitCode).ConfigureAwait(false);
        }

        var rows = new List<string> { $"{"onyx",-10}{OnyxStateText(status)}" };
        if (status.Url is not null)
        {
            rows.Add($"{"url",-10}{status.Url}");
            rows.Add($"{"running",-10}{status.RunningVersion}");
        }

        rows.Add($"{"pinned",-10}{status.PinnedVersion} ({status.PinnedCommit[..7]})");
        rows.AddRange(status.Checks.Select(c => $"{c.Name,-10}{Word(c.State)} {c.Detail}{(c.Problem is null ? string.Empty : $" ({c.Problem.Code}: {c.Problem.Message})")}"));
        return await output.Text(string.Join('\n', rows) + "\n", status.ExitCode).ConfigureAwait(false);
    }

    private static string OnyxStateText(OnyxStatusResponse status) => status.State switch
    {
        OnyxState.NotSetUp => "not set up (run `sindri onyx up`)",
        OnyxState.Stopped => "stopped (run `sindri onyx up` to start it)",
        OnyxState.Running => "running",
        OnyxState.Unknown => $"unknown (Docker isn't usable here: {status.Checks.FirstOrDefault(static c => c.Name == "docker")?.Detail})",
        _ => status.State.ToString(),
    };

    internal async Task<int> Approve(string? hashText, bool interactive, TextReader input, CancellationToken ct)
    {
        ProfileHash? hash = hashText is not null && ProfileHash.TryFrom(hashText, out var parsed) ? parsed : null;
        if (Precheck(hashText, hash, interactive) is { } refused)
        {
            return await output.Error(refused).ConfigureAwait(false);
        }

        var answer = await BeforeTheCall(hash, input, ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        var approved = await client.Post<ApproveProfileRequest, ApproveProfileResponse>("/sindri/profile/approve", new ApproveProfileRequest(hash, answer.Value), ct).ConfigureAwait(false);
        return approved.IsError ? await output.Error(approved.Error).ConfigureAwait(false) : await output.Text(ApprovalText(approved.Value.Value, hash is null), approved.Value.Value.Approved ? 0 : 1).ConfigureAwait(false);
    }

    /// <summary>A malformed hash, or a hash without a terminal: refused before any call, and before starting anything.</summary>
    private static Kernel.Error? Precheck(string? hashText, ProfileHash? hash, bool interactive) => (hashText, hash) switch
    {
        ({ } text, null) => SindriErrors.InvalidInput.With($"'{text}' is not a profile hash (64 hex digits)"),
        (_, { }) when !interactive => SindriErrors.ProfileNeedsTerminal.With("approving needs an interactive terminal: run it yourself and type the action the challenge asks for"),
        _ => null,
    };

    /// <summary>
    /// Starts the control plane if this CLI may (API spec §7), then, to approve, asks for the 'approve profile &lt;hash6&gt;'
    /// challenge and reads what the human types (S1-D5). A session gets no challenge: it sends the bare request, and the
    /// command chain refuses it with SND-AUTH-002 naming ApproveProfile.
    /// </summary>
    private async Task<Kernel.Result<ChallengeAnswer?>> BeforeTheCall(ProfileHash? hash, TextReader input, CancellationToken ct)
    {
        var down = ensureRunning is null ? null : await ensureRunning(ct).ConfigureAwait(false);
        if (down is not null)
        {
            return down;
        }

        if (hash is not { } h || client.SessionReason is not null)
        {
            return Kernel.Results.Success<ChallengeAnswer?>(value: null);
        }

        var issued = await client.Post<IssueChallengeRequest, IssueChallengeResponse>("/sindri/workflow/challenges",
            new IssueChallengeRequest(ChallengeAction.ApproveProfile(h).Text), ct).ConfigureAwait(false);
        if (issued.IsError)
        {
            return issued.Error;
        }

        await output.Out.WriteAsync($"Type '{issued.Value.Value.Action}' to approve it: ").ConfigureAwait(false);
        var typed = (await input.ReadLineAsync(ct).ConfigureAwait(false))?.Trim() ?? string.Empty;
        return new ChallengeAnswer(issued.Value.Value.Challenge, typed);
    }

    private static string ApprovalText(ApproveProfileResponse a, bool preview) => (preview, a.Approved) switch
    {
        (_, true) when preview => $"Approved already: {a.Hash.Value}\n",
        (true, _) => $"hash {a.Hash.Value}\nchanged since the last approval: {(a.Changes.Count == 0 ? "nothing" : string.Join(", ", a.Changes))}\nto approve: sindri profile approve {a.Hash.Value}\n",
        _ => $"Approved {a.Hash.Value}\n",
    };

    private static string Word(CheckState state) => state switch
    {
        CheckState.Ok => "ok",
        CheckState.Warn => "warn",
        CheckState.Fail => "fail",
        _ => "?",
    };
}
