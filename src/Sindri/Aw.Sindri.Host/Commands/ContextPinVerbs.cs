using System.Globalization;
using System.Text;
using Aw.Context.Core;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.GetJob;
using Aw.Sindri.Features.PinConcepts;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri context pin set|remove|list' (concept pins spec §3.6, "CLI and agents"). 'set' and 'remove' start the PinConcepts job and follow it as 'repo add' does (Ctrl-C cancels the job, a second Ctrl-C leaves; --detach prints
/// the job id and returns); a human starts the control plane unless --no-start, a session never does. They only write the profile and never approve: the last line names the hash to approve. 'list' reads the live profile
/// in process, like 'profile validate', so it works with the control plane down. Exit 0 written, 1 the job failed, Sindri is not running or a session was refused, 2 invalid input.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="follower">Follows the job the start answers.</param>
/// <param name="services">Gives the profile source 'list' reads.</param>
/// <param name="ensureRunning">Starts the control plane (true: this verb may launch it) or names why not; null: never start it.</param>
internal sealed class ContextPinVerbs(ControlPlaneClient client, Output output, JobFollower follower, IServiceProvider services, Func<bool, CancellationToken, Task<Error?>>? ensureRunning = null)
{
    private static readonly string[] _refusals = ["SND-CONTEXT-171", "SND-CONTEXT-172", "SND-CONTEXT-173", "SND-CONTEXT-174", "SND-CONTEXT-175", "SND-CONTEXT-176", "SND-CONTEXT-177", "SND-PROFILE-003"];

    internal async Task<int> Run(string[] args, CancellationToken interrupt, CancellationToken again)
    {
        ArgumentNullException.ThrowIfNull(args);
        var parsed = ContextPinArguments.Parse(args);
        if (parsed.IsError)
        {
            return await output.Error(parsed.Error).ConfigureAwait(false);
        }

        var line = parsed.Value;
        if (line.Verb == ContextPinArguments.Verb.List)
        {
            return await List(line.Json, interrupt).ConfigureAwait(false);
        }

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

        var started = await client.Post<PinConceptsRequest, JobStartedResponse>("/sindri/context/pins", request.Value, interrupt).ConfigureAwait(false);
        if (started.IsError)
        {
            return ContextExit.For(started.Error, await output.Error(started.Error).ConfigureAwait(false));
        }

        return await Finish(started.Value, line, (interrupt, again)).ConfigureAwait(false);
    }

    private async Task<int> Finish(ApiAnswer<JobStartedResponse> started, ContextPinArguments.Parsed line, (CancellationToken Interrupt, CancellationToken Again) signals)
    {
        if (!line.Detach)
        {
            var followed = await follower.Follow(started.Value, line.Json, signals.Interrupt, signals.Again, Describe).ConfigureAwait(false);
            return followed == 1 ? await RefusedExit(started.Value, signals.Interrupt).ConfigureAwait(false) : followed;
        }

        return line.Json
            ? await output.Raw(started.Body, 0).ConfigureAwait(false)
            : await output.Text($"job {started.Value.JobId.Value} started\n", 0).ConfigureAwait(false);
    }

    // A job that failed with one of the pin refusals (SND-CONTEXT-171..177, SND-PROFILE-003) is invalid input: exit 2, as the spec's error table says. Any other end stays 1.
    private async Task<int> RefusedExit(JobStartedResponse started, CancellationToken ct)
    {
        var job = await client.Get<JobView>(started.Links.Job, ct).ConfigureAwait(false);
        return job.IsSuccess && job.Value.Value.Code is { } code && _refusals.Contains(code, StringComparer.Ordinal) ? 2 : 1;
    }

    // What the flags ask the job to write: a flag left out keeps the stored value (null), a given list replaces it, --clear-* empties it.
    private async Task<Result<PinConceptsRequest>> Request(ContextPinArguments.Parsed line, CancellationToken ct)
    {
        if (line.Verb == ContextPinArguments.Verb.Remove)
        {
            if (line.Id is not { } id)
            {
                return SindriErrors.InvalidInput.With("context pin remove takes one pin id");
            }

            return new PinConceptsRequest(Remove: [id], IfHash: line.IfHash, WithFolderRules: line.WithFolderRules);
        }

        if (line.FromFile is { } path)
        {
            var read = await FromFile(path, ct).ConfigureAwait(false);
            return read.IsError ? read.Error : new PinConceptsRequest(Set: read.Value, IfHash: line.IfHash);
        }

        var set = new PinSet(line.Name, line.Id, line.Summary, Replacement(line.ClearAliases, line.Aliases), Replacement(line.ClearFeatures, line.Features));
        return new PinConceptsRequest(Set: [set], IfHash: line.IfHash);
    }

    // A flag left out is null (keep the stored list), a given list replaces it, --clear-* is the empty list.
    private static List<string>? Replacement(bool clear, List<string>? given) => clear ? [] : given;

    private async Task<Result<IReadOnlyList<PinSet>>> FromFile(string path, CancellationToken ct)
    {
        string text;
        try
        {
            text = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ContextErrors.PinLineUnreadable.With($"can't read '{path}': {e.Message}");
        }

        var list = ConceptList.Parse(text);
        if (list.NamelessLine is { } nameless)
        {
            return ContextErrors.PinLineUnreadable.With(string.Create(CultureInfo.InvariantCulture, $"line {nameless} has no name (fix: start the line or block with the concept's name)"));
        }

        if (list.Skipped > 0)
        {
            await output.Notice(string.Create(CultureInfo.InvariantCulture, $"the list has more than {ConceptList.Max} concepts: pinning the first {ConceptList.Max}, {list.Skipped} left out")).ConfigureAwait(false);
        }

        if (list.Concepts.Count == 0)
        {
            return SindriErrors.InvalidInput.With($"'{path}' has no concepts (fix: one concept per line, or blocks with a blank line between)");
        }

        IReadOnlyList<PinSet> sets = [.. list.Concepts.Select(static c => new PinSet(c.Name, Summary: c.Summary, Aliases: c.Aliases.Count == 0 ? null : c.Aliases))];
        return Kernel.Results.Success(sets);
    }

    /// <summary>The final job's lines: what was pinned or unpinned, then the hash to approve; nothing when the result isn't there or doesn't read.</summary>
    private string? Describe(JobView job)
    {
        if (job.Result is not { } result)
        {
            return null;
        }

        var read = output.Read<PinConceptsResult>(Encoding.UTF8.GetBytes(result.GetRawText()));
        return read.IsSuccess ? Text(read.Value) : null;
    }

    internal static string Text(PinConceptsResult done)
    {
        ArgumentNullException.ThrowIfNull(done);
        var wait = $" (waiting for your approval: sindri profile approve {done.Hash.Value})\n";
        var text = new StringBuilder();
        var pinned = done.Pins.Where(static p => !string.Equals(p.Action, "removed", StringComparison.Ordinal)).Select(static p => p.Id.Value).ToList();
        var removed = done.Pins.Where(static p => string.Equals(p.Action, "removed", StringComparison.Ordinal)).Select(static p => p.Id.Value).ToList();
        _ = pinned.Count > 0 ? text.Append("pinned ").AppendJoin(", ", pinned).Append(wait) : text;
        _ = removed.Count > 0 ? text.Append("unpinned ").AppendJoin(", ", removed).Append(wait) : text;
        _ = done.Dropped.Count > 0 ? text.Append("dropped folder rules: ").AppendJoin(", ", done.Dropped).Append('\n') : text;
        return text.ToString();
    }

    private async Task<int> List(bool json, CancellationToken ct)
    {
        var loaded = await services.GetRequiredService<IProfileSource>().Load(ct).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return await output.Error(loaded.Error).ConfigureAwait(false);
        }

        var view = PinList.From(loaded.Value);
        return json ? await output.Json(view, 0).ConfigureAwait(false) : await output.Text(PinList.Render(view), 0).ConfigureAwait(false);
    }
}
