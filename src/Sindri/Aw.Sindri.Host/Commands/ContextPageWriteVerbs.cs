using System.Text;
using Aw.Context.Core;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.GetJob;
using Aw.Sindri.Features.SetPages;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri context page set|remove' (pages spec §3.6, "CLI"). Both start the SetPages job and follow it as 'context pin set' does (Ctrl-C cancels the job, a second Ctrl-C leaves; --detach prints the job id and returns); a human
/// starts the control plane unless --no-start, a session never does. They only write the profile and never approve: the last line names the hash to approve. '--dry-run' plans and checks exactly as a write does and prints the rows that
/// would change. Exit 0 written, 1 attention (an unknown name or key, no map, Sindri not running, a session refused), 2 invalid input or a refused write.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="follower">Follows the job the start answers.</param>
/// <param name="services">Gives the profile text parser '--from-file' reads with.</param>
/// <param name="ensureRunning">Starts the control plane (true: this verb may launch it) or names why not; null: never start it.</param>
internal sealed class ContextPageWriteVerbs(ControlPlaneClient client, Output output, JobFollower follower, IServiceProvider services, Func<bool, CancellationToken, Task<Error?>>? ensureRunning = null)
{
    internal async Task<int> Run(string[] args, CancellationToken interrupt, CancellationToken again)
    {
        ArgumentNullException.ThrowIfNull(args);
        var parsed = ContextPageArguments.Parse(args);
        if (parsed.IsError)
        {
            return await output.Error(parsed.Error).ConfigureAwait(false);
        }

        var line = parsed.Value;
        var request = await Request(line, interrupt).ConfigureAwait(false);
        if (request.IsError)
        {
            return await output.Error(request.Error).ConfigureAwait(false);
        }

        var down = ensureRunning is null ? null : await ensureRunning(!line.NoStart, interrupt).ConfigureAwait(false);
        if (down is not null)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        var started = await client.Post<SetPagesRequest, JobStartedResponse>("/sindri/context/pages", request.Value, interrupt).ConfigureAwait(false);
        if (started.IsError)
        {
            return ContextExit.For(started.Error, await output.Error(started.Error).ConfigureAwait(false));
        }

        return await Finish(started.Value, line, (interrupt, again)).ConfigureAwait(false);
    }

    private async Task<int> Finish(ApiAnswer<JobStartedResponse> started, ContextPageArguments.Parsed line, (CancellationToken Interrupt, CancellationToken Again) signals)
    {
        if (!line.Detach)
        {
            var followed = await follower.Follow(started.Value, line.Json, signals.Interrupt, signals.Again, Describe).ConfigureAwait(false);
            return followed == 1 ? await FailedExit(started.Value, signals.Interrupt).ConfigureAwait(false) : followed;
        }

        return line.Json
            ? await output.Raw(started.Body, 0).ConfigureAwait(false)
            : await output.Text($"job {started.Value.JobId.Value} started\n", 0).ConfigureAwait(false);
    }

    // A job that failed with an attention code is exit 1; with a page refusal (SND-CONTEXT-252 to 258 that aren't attention) or a moved profile (SND-PROFILE-003) it is invalid input, exit 2. Any other end stays 1.
    private async Task<int> FailedExit(JobStartedResponse started, CancellationToken ct)
    {
        var job = await client.Get<JobView>(started.Links.Job, ct).ConfigureAwait(false);
        if (job.IsError || job.Value.Value.Code is not { } code || ContextErrors.Attention.Contains(code))
        {
            return 1;
        }

        return code.StartsWith("SND-CONTEXT-25", StringComparison.Ordinal) || string.Equals(code, SindriErrors.ProfileHashMismatch.Code, StringComparison.Ordinal) ? 2 : 1;
    }

    private async Task<Result<SetPagesRequest>> Request(ContextPageArguments.Parsed line, CancellationToken ct)
    {
        if (line.Verb == ContextPageArguments.Verb.Remove)
        {
            return line.Id is { } id ? new SetPagesRequest(Remove: [id], IfHash: line.IfHash) : SindriErrors.InvalidInput.With("context page remove takes one page id");
        }

        if (line.FromFile is { } path)
        {
            var read = await FromFile(path, ct).ConfigureAwait(false);
            return read.IsError ? read.Error : new SetPagesRequest(Set: read.Value, IfHash: line.IfHash, DryRun: line.DryRun);
        }

        var page = new PageSettings(line.Id, line.Name, line.Repo, line.Surface, line.Actions.Count == 0 ? null : line.Actions);
        return new SetPagesRequest(Set: [page], IfHash: line.IfHash, DryRun: line.DryRun);
    }

    // The file is a profile fragment: 'index: pages: [...]' (the shape the profile has) or a bare 'pages:' list. It is read with the profile's own reader, so a typo is named with its key path.
    private async Task<Result<IReadOnlyList<PageSettings>>> FromFile(string path, CancellationToken ct)
    {
        string text;
        try
        {
            text = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.InvalidInput.With($"can't read '{path}': {e.Message}");
        }

        var parsed = services.GetRequiredService<IProfileTextParser>().Parse(Wrapped(text), path);
        if (parsed.IsError)
        {
            return parsed.Error;
        }

        var pages = parsed.Value.Profile.Index?.Pages ?? [];
        return pages.Count == 0
            ? SindriErrors.InvalidInput.With($"'{path}' has no pages (fix: an 'index:' with 'pages:', or a bare 'pages:' list)")
            : Kernel.Results.Success<IReadOnlyList<PageSettings>>(pages);
    }

    // The text as a whole profile: a bare 'pages:' list goes under 'index:'.
    private static string Wrapped(string text)
    {
        const string head = "schemaVersion: 1\nuser: pages-file\n";
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var bare = lines.Any(static l => l.StartsWith("pages:", StringComparison.Ordinal)) && !lines.Any(static l => l.StartsWith("index:", StringComparison.Ordinal));
        return head + (bare ? "index:\n" + string.Join('\n', lines.Select(static l => l.Length == 0 ? l : "  " + l)) : text);
    }

    /// <summary>The final job's lines; nothing when the result isn't there or doesn't read.</summary>
    private string? Describe(JobView job)
    {
        if (job.Result is not { } result)
        {
            return null;
        }

        var read = output.Read<SetPagesResult>(Encoding.UTF8.GetBytes(result.GetRawText()));
        return read.IsSuccess ? Text(read.Value) : null;
    }

    internal static string Text(SetPagesResult done)
    {
        ArgumentNullException.ThrowIfNull(done);
        var text = new StringBuilder();
        if (!done.Written)
        {
            _ = done.Diff.Count == 0 ? text.AppendLine("no page row changes") : text;
            foreach (var row in done.Diff)
            {
                _ = text.AppendLine(row);
            }

            return text.AppendLine("dry run: nothing written").ToString();
        }

        foreach (var page in done.Pages)
        {
            _ = text.Append(page.Action).Append(' ').Append(page.Id.Value).Append(" (").Append(page.Name).AppendLine(")");
        }

        return text.Append("waiting for your approval: sindri profile approve ").AppendLine(done.Hash.Value).ToString();
    }
}
