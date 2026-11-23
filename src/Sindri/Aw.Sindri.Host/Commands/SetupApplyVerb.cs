using System.Globalization;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Features.GetSetup;
using Aw.Sindri.Features.ScanRepos;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri setup --apply [--repo &lt;path&gt;]... [--timeout &lt;duration&gt;] [--json]' (first-run spec decision 10, §5.1): does every setup step that needs no human. It starts serve when it is down, adds each
/// repo named by --repo through the AddRepo job (with none named, the known repos a scan of the home finds; SND-SETUP-010 when there is none), waits for those jobs, runs the setup service at once and
/// waits for it up to --timeout (default 120 s; 0 starts it and doesn't wait), then prints what it did and the steps. It never signs in and never restarts. A repo the profile names already is "already",
/// not an error. Exit 0 when every repo is added or already there and the pass is done, skipped or refused; 1 when a repo failed, serve didn't start, or the pass timed out (SND-SETUP-015) or failed
/// (SND-SETUP-016); 2 on usage or SND-SETUP-010.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verb writes.</param>
/// <param name="ensureRunning">Starts serve when it is down: null once it runs, otherwise why it didn't start.</param>
/// <param name="clock">Times the waits.</param>
internal sealed class SetupApplyVerb(ControlPlaneClient client, Output output, Func<bool, CancellationToken, Task<Error?>> ensureRunning, TimeProvider clock)
{
    internal const string Usage = "setup --apply [--repo <path>]... [--timeout <duration>] [--json]";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        if (SetupApplyLine.Parse(args) is not { } line)
        {
            return await output.Error(SindriErrors.InvalidInput.With($"'setup {string.Join(' ', args)}' isn't this command ({Usage})")).ConfigureAwait(false);
        }

        var before = await client.Get<GetSetupResponse>("/sindri/setup", ct).ConfigureAwait(false);   // the cheap look: with serve down it answers SND-API-001 without a call
        var serveStarted = before.IsError && string.Equals(before.Error.Code, SindriErrors.NotRunning.Code, StringComparison.Ordinal);
        if (await ensureRunning(true, ct).ConfigureAwait(false) is { } down)
        {
            _ = await output.Error(down).ConfigureAwait(false);
            return 1;
        }

        var repos = await Repos(line, ct).ConfigureAwait(false);
        if (repos.IsError)
        {
            return await output.Error(repos.Error).ConfigureAwait(false);
        }

        var jobs = new SetupApplyJobs(client, clock);
        var (added, already, failed) = await AddAll(jobs, repos.Value, ct).ConfigureAwait(false);
        var pass = await jobs.Pass(line.Timeout, ct).ConfigureAwait(false);
        var steps = await client.Get<GetSetupResponse>("/sindri/setup", ct).ConfigureAwait(false);
        var document = new SetupApplyDocument(added, already, failed, serveStarted, pass, steps.IsSuccess ? steps.Value.Value.Steps : []);
        await PassLine(pass, line.Timeout).ConfigureAwait(false);
        var code = failed.Count > 0 || pass.Bad ? 1 : 0;
        return line.Json ? await output.Json(document, code).ConfigureAwait(false) : await output.Text(Text(document, steps.IsSuccess ? steps.Value.Value : null), code).ConfigureAwait(false);
    }

    // The repos to add: the ones named; with none named, the known ones a scan of the home finds (those the profile names already go under "already"); SND-SETUP-010 when none is known.
    private async Task<Result<RepoList>> Repos(SetupApplyLine line, CancellationToken ct)
    {
        if (line.Repos.Count > 0)
        {
            return new RepoList(line.Repos, []);
        }

        List<string> add = [];
        List<string> already = [];
        await foreach (var text in client.Lines("/sindri/setup/repos", ct).ConfigureAwait(false))
        {
            if (text.IsError)
            {
                return text.Error;
            }

            if (output.Read<RepoScanRow>(Encoding.UTF8.GetBytes(text.Value)) is { IsSuccess: true, Value: { Known: true } row })
            {
                (row.InProfile ? already : add).Add(row.Path);
            }
        }

        if (add.Count + already.Count == 0)
        {
            return SindriErrors.SetupNoRepoToAdd.With(SindriErrors.SetupNoRepoToAdd.Title);
        }

        return new RepoList(add, already);
    }

    private static async Task<(List<string> Added, List<string> Already, List<SetupApplyFailure> Failed)> AddAll(SetupApplyJobs jobs, RepoList repos, CancellationToken ct)
    {
        List<string> added = [];
        List<string> already = [.. repos.Already];
        List<SetupApplyFailure> failed = [];
        foreach (var path in repos.Add.Distinct(StringComparer.Ordinal))
        {
            var error = await jobs.Add(path, ct).ConfigureAwait(false);
            if (error is null)
            {
                added.Add(path);
            }
            else if (SetupApplyJobs.Already(error))
            {
                already.Add(path);
            }
            else
            {
                failed.Add(new SetupApplyFailure(path, error.Code, error.Message));
            }
        }

        return (added, already, failed);
    }

    // A pass that timed out or failed gets its one line on stderr (first-run spec §5.1), in --json too.
    private async Task PassLine(SetupPassOutcome pass, TimeSpan timeout)
    {
        var seconds = ((int)timeout.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        if (string.Equals(pass.State, SetupPassOutcome.Timeout, StringComparison.Ordinal))
        {
            _ = await output.Error(SindriErrors.SetupPassTimedOut.With($"the setup pass didn't finish in {seconds} s; it keeps running as {pass.JobId} (fix: sindri jobs show {pass.JobId}, then sindri setup)")).ConfigureAwait(false);
        }
        else if (string.Equals(pass.State, SetupPassOutcome.Failed, StringComparison.Ordinal))
        {
            var fix = pass.JobId is { } job ? $" (fix: sindri jobs show {job})" : " (fix: sindri services run setup)";
            _ = await output.Error(SindriErrors.SetupPassFailed.With($"the setup pass failed: {pass.Error}{fix}")).ConfigureAwait(false);
        }
    }

    private static string Text(SetupApplyDocument document, GetSetupResponse? setup)
    {
        var text = new StringBuilder();
        _ = document.ServeStarted ? text.Append("started Sindri\n") : text;
        foreach (var path in document.Added)
        {
            _ = text.Append("added     ").Append(path).Append('\n');
        }

        foreach (var path in document.Already)
        {
            _ = text.Append("already   ").Append(path).Append('\n');
        }

        foreach (var failure in document.Failed)
        {
            _ = text.Append("failed    ").Append(failure.Path).Append(": ").Append(failure.Code).Append(' ').Append(failure.Message).Append('\n');
        }

        _ = text.Append(PassText(document.SetupPass)).Append('\n');
        return setup is null ? text.ToString() : text.Append(SetupText.Render(setup)).ToString();
    }

    private static string PassText(SetupPassOutcome pass) => pass.State switch
    {
        SetupPassOutcome.Done => string.Create(CultureInfo.InvariantCulture, $"setup pass done in {pass.Seconds} s ({pass.JobId})"),
        SetupPassOutcome.Skipped => $"setup pass started ({pass.JobId}); not waiting for it (--timeout 0)",
        SetupPassOutcome.Refused => "setup pass: this caller may not run services; it runs on its own within 60 s",
        SetupPassOutcome.Timeout => $"setup pass still running ({pass.JobId})",
        _ => "setup pass failed",
    };

    private sealed record RepoList(IReadOnlyList<string> Add, IReadOnlyList<string> Already);
}
