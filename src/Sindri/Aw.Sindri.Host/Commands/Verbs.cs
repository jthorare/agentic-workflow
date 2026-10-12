using System.Globalization;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Features.StopProcess;
using Aw.Sindri.Features.ValidateProfile;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The client verbs. status, stop and profile approve call the running control plane's API; profile validate and doctor
/// run the same handlers in this process, read-only, so they work while it is down.
/// </summary>
internal sealed class Verbs(ControlPlaneClient client, Core.Control.IControlPlaneProbe probe, Output output, IServiceProvider scope)
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

        var code = status.Value.Profile.NeedsApproval ? 1 : 0;
        return json
            ? await output.Json(status.Value, code).ConfigureAwait(false)
            : await output.Text(SummaryText.Render(status.Value, found.Value.Endpoint, showUi: false), code).ConfigureAwait(false);
    }

    internal async Task<int> Stop(CancellationToken ct)
    {
        var stopped = await client.Post<StopProcessRequest, StopProcessResponse>("/sindri/stop", body: null, ct).ConfigureAwait(false);
        return stopped.IsError
            ? await output.Error(stopped.Error).ConfigureAwait(false)
            : await output.Text(string.Create(CultureInfo.InvariantCulture, $"stopped (pid {stopped.Value.Pid}, epoch {stopped.Value.Epoch} released)\n"), 0).ConfigureAwait(false);
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

    internal async Task<int> Doctor(bool json, CancellationToken ct)
    {
        var report = (await scope.GetRequiredService<IService<RunDoctorRequest, RunDoctorResponse>>().Handle(new RunDoctorRequest(), ct).ConfigureAwait(false)).Value;
        if (json)
        {
            return await output.Json(report, report.ExitCode).ConfigureAwait(false);
        }

        var lines = report.Checks.Select(c => $"{Word(c.State),-5} {c.Name,-8} {c.Detail}{(c.Fix is null ? string.Empty : $" (fix: {c.Fix})")}");
        return await output.Text(string.Join('\n', lines) + "\n", report.ExitCode).ConfigureAwait(false);
    }

    internal async Task<int> Approve(string? hashText, bool interactive, TextReader input, CancellationToken ct)
    {
        ProfileHash? hash = hashText is not null && ProfileHash.TryFrom(hashText, out var parsed) ? parsed : null;
        if (hashText is not null && hash is null)
        {
            return await output.Error(SindriErrors.InvalidInput.With($"'{hashText}' is not a profile hash (64 hex digits)")).ConfigureAwait(false);
        }

        var refusal = hash is { } h ? await Confirmation(h, interactive, input).ConfigureAwait(false) : null;
        if (refusal is not null)
        {
            return await output.Error(refusal).ConfigureAwait(false);
        }

        var answer = await client.Post<ApproveProfileRequest, ApproveProfileResponse>("/sindri/profile/approve", new ApproveProfileRequest(hash, hash is not null), ct).ConfigureAwait(false);
        return answer.IsError ? await output.Error(answer.Error).ConfigureAwait(false) : await output.Text(ApprovalText(answer.Value, hash is null), answer.Value.Approved ? 0 : 1).ConfigureAwait(false);
    }

    private async Task<Kernel.Error?> Confirmation(ProfileHash hash, bool interactive, TextReader input)
    {
        if (!interactive)
        {
            return SindriErrors.ProfileNeedsTerminal.With("approving needs an interactive terminal: run it yourself and type the first 6 characters of the hash");
        }

        var typed = await Prompt(hash, input).ConfigureAwait(false);
        return string.Equals(typed, hash.Abbreviated, StringComparison.Ordinal)
            ? null
            : SindriErrors.ProfileNeedsTerminal.With($"you typed '{typed}', not the hash's first 6 characters; nothing was approved");
    }

    private async Task<string?> Prompt(ProfileHash hash, TextReader input)
    {
        await output.Out.WriteAsync($"Type the first 6 characters of {hash.Value} to approve it: ").ConfigureAwait(false);
        return (await input.ReadLineAsync().ConfigureAwait(false))?.Trim();
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
