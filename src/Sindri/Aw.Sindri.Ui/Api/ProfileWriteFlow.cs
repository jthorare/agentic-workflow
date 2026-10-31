using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Ui.Api.Jobs;

namespace Aw.Sindri.Ui.Api;

/// <summary>
/// The write-then-approve flow every profile sheet shares (concept pins spec §3.6): start the write job once, poll it, and approve the hash the job returned only when the live profile is still that hash and nothing but the
/// caller's own key paths waits. Otherwise the change stays for Review and approve.
/// </summary>
/// <param name="api">Sindri's API.</param>
/// <param name="apiUrl">The control plane's API address now, or null when none is known.</param>
/// <param name="clock">The clock for the job poll.</param>
internal sealed class ProfileWriteFlow(ISindriApi api, Func<Uri?> apiUrl, TimeProvider clock)
{
    /// <summary>The code of a <see cref="ProfileWritten"/> that failed because its job still ran after the wait (not a server code).</summary>
    internal const string TimedOut = "AW-UI-TIMEOUT";

    private const string Clash = "SND-PROFILE-003";
    private const string RefusedPrefix = "SND-CONTEXT-";
    private const int MaxPolls = 120;
    private const string JobNotFound = "SND-JOB-404";

    // The job is recorded before the POST answers but the jobs view folds it after, so a first read can miss it: read again; a not-found counts as "not yet" for MaxNotFound reads in total, then reads as still running like any failed poll.
    private const int MaxNotFound = 8;
    private static readonly TimeSpan _pollEvery = TimeSpan.FromMilliseconds(250);

    /// <summary>Reads the live profile's hash and the key paths that wait for approval.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The state, or null when the profile can't be read.</returns>
    public async Task<ProfileWaiting?> Waiting(CancellationToken cancellationToken)
    {
        if (apiUrl() is not { } url)
        {
            return null;
        }

        var read = await api.ProfileSummary(url, cancellationToken).ConfigureAwait(true);
        return read.IsSuccess ? new ProfileWaiting(read.Value.Live, read.Value.ChangedPaths ?? []) : null;
    }

    /// <summary>Starts a write job once, waits up to 30 s for it, and approves its hash when asked and safe.</summary>
    /// <param name="start">Posts the write job; called once.</param>
    /// <param name="mine">Says whether a waiting key path is one this write made.</param>
    /// <param name="approve">True when the button read "Save and approve".</param>
    /// <param name="cancellationToken">Cancels the wait (the write itself is sent once).</param>
    /// <returns>What happened.</returns>
    public async Task<ProfileWritten> Run(Func<Uri, CancellationToken, Task<Result<JobStarted>>> start, Func<string, bool> mine, bool approve, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(mine);
        if (apiUrl() is not { } url)
        {
            return new ProfileWritten(ProfileWriteState.Failed);
        }

        var started = await start(url, cancellationToken).ConfigureAwait(true);
        if (!started.IsSuccess)
        {
            return Ended(started.Error.Code, started.Error.Message);
        }

        var view = await Wait(url, started.Value.JobId, cancellationToken).ConfigureAwait(true);
        if (view is not { State: "succeeded" })
        {
            return view is null ? new ProfileWritten(ProfileWriteState.Failed, Code: TimedOut) : Ended(view.Code, view.Summary);
        }

        var hash = HashOf(view.Result);
        return approve && hash is not null ? await Approve(url, hash, mine, cancellationToken).ConfigureAwait(true) : new ProfileWritten(ProfileWriteState.Waiting, hash);
    }

    // Approves the hash the job returned, never a fresh read's: only when the live profile is still that hash and nothing else waits.
    private async Task<ProfileWritten> Approve(Uri url, string hash, Func<string, bool> mine, CancellationToken cancellationToken)
    {
        var waiting = await Waiting(cancellationToken).ConfigureAwait(true);
        if (waiting is null || !string.Equals(waiting.LiveHash, hash, StringComparison.Ordinal) || !waiting.OnlyMine(mine))
        {
            return new ProfileWritten(ProfileWriteState.Waiting, hash);
        }

        var approved = await api.ApproveProfile(url, new ApproveProfileRequest(hash), cancellationToken).ConfigureAwait(true);
        if (approved.IsSuccess)
        {
            return new ProfileWritten(ProfileWriteState.Approved, hash);
        }

        // The write job already landed with this hash, so any approve error (a clash included) leaves it waiting for Review and approve, never failed.
        return new ProfileWritten(ProfileWriteState.Waiting, hash, approved.Error.Code, approved.Error.Message);
    }

    // The job's last read: its view once it ended, or no view when it still ran after the wait or a poll failed (the job may still land, so both read as still running).
    // A job not found counts as "not yet" for MaxNotFound reads; past that it reads as still running like any failed poll (the POST was accepted, so a stalled jobs view may still fold it).
    private async Task<JobView?> Wait(Uri url, string jobId, CancellationToken cancellationToken)
    {
        var notFound = 0;
        for (var poll = 0; poll < MaxPolls; poll++)
        {
            var read = await api.Job(url, jobId, cancellationToken).ConfigureAwait(true);
            if (!read.IsSuccess)
            {
                if (!string.Equals(read.Error.Code, JobNotFound, StringComparison.Ordinal))
                {
                    return null;
                }

                if (++notFound >= MaxNotFound)
                {
                    return null;
                }
            }
            else if (read.Value.State is not ("running" or "waiting"))   // a job parked on a signal ('model-limit') has not ended either
            {
                return read.Value;
            }

            await Task.Delay(_pollEvery, clock, cancellationToken).ConfigureAwait(true);
        }

        return null;
    }

    // What an error id means for a sheet: a moved profile, a refusal with the server's sentence, or anything else.
    private static ProfileWritten Ended(string? code, string? message)
    {
        if (string.Equals(code, Clash, StringComparison.Ordinal))
        {
            return new ProfileWritten(ProfileWriteState.Clash, Code: code, Message: message);
        }

        var refused = code is not null && code.StartsWith(RefusedPrefix, StringComparison.Ordinal);
        return new ProfileWritten(refused ? ProfileWriteState.Refused : ProfileWriteState.Failed, Code: code, Message: message);
    }

    private static string? HashOf(JsonElement? result) =>
        result is { ValueKind: JsonValueKind.Object } r && r.TryGetProperty("hash", out var hash) && hash.ValueKind == JsonValueKind.String ? hash.GetString() : null;
}
