using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.CancelJob;
using Aw.Sindri.Features.GetJob;
using Aw.Sindri.Features.ListJobs;
using Aw.Sindri.Features.ResumeJob;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri jobs', 'jobs show', 'jobs cancel' and 'jobs resume' (API P6 unit H): clients of the job routes. Human verbs start the control plane unless --no-start; inside
/// an agent session they never do (API spec §7). The reads exit 1 while the views rebuild or stall, so a script doesn't read a partial list as the truth.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="ensureRunning">Starts the control plane (true: this verb may launch it) or names why not; null: never start it.</param>
/// <param name="clock">The clock 'jobs show' dates a model call by (a row from another day carries its date); the system clock when null.</param>
internal sealed class JobVerbs(ControlPlaneClient client, Output output, Func<bool, CancellationToken, Task<Error?>>? ensureRunning = null, TimeProvider? clock = null)
{
    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        var parsed = JobCommandLine.Parse(args);
        if (parsed.IsError)
        {
            return await output.Error(parsed.Error).ConfigureAwait(false);
        }

        var command = parsed.Value;
        var down = ensureRunning is null ? null : await ensureRunning(!command.NoStart, ct).ConfigureAwait(false);
        if (down is not null)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        return command.Verb switch
        {
            "show" => await Show(command, ct).ConfigureAwait(false),
            "cancel" => await Cancel(command, ct).ConfigureAwait(false),
            "resume" => await Resume(command, ct).ConfigureAwait(false),
            _ => await List(command, ct).ConfigureAwait(false),
        };
    }

    private async Task<int> List(JobCommandLine command, CancellationToken ct)
    {
        var answer = await client.GetView<JobList>("/sindri/jobs" + UrlQuery.Of([("state", command.State), ("kind", command.Kind)]), ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        var code = answer.Value.Value.AsOf.Rebuilding || answer.Value.Value.AsOf.Stalled ? 1 : 0;
        return command.Json
            ? await output.Raw(answer.Value.Body, code).ConfigureAwait(false)
            : await output.Text(JobText.Render(answer.Value.Value), code).ConfigureAwait(false);
    }

    private async Task<int> Show(JobCommandLine command, CancellationToken ct)
    {
        var answer = await client.GetView<JobView>("/sindri/jobs/" + Uri.EscapeDataString(command.Job ?? string.Empty), ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        var code = answer.Value.Value.AsOf.Rebuilding || answer.Value.Value.AsOf.Stalled ? 1 : 0;
        return command.Json
            ? await output.Raw(answer.Value.Body, code).ConfigureAwait(false)
            : await output.Text(JobText.Render(answer.Value.Value, clock ?? TimeProvider.System) + await PublishLink(answer.Value.Value, ct).ConfigureAwait(false), code).ConfigureAwait(false);
    }

    // A map refresh that ended held for the code index points at the publish job that finished it; a failed publish read leaves the job's own text alone.
    private async Task<string> PublishLink(JobView job, CancellationToken ct)
    {
        if (HeldRefreshLinks.HeldVersion(job) is not { } version)
        {
            return string.Empty;
        }

        var publishes = await client.GetView<JobList>("/sindri/jobs" + UrlQuery.Of([("kind", HeldRefreshLinks.PublishJobKind)]), ct).ConfigureAwait(false);
        return publishes.IsError ? string.Empty : HeldRefreshLinks.Publish(job, version, publishes.Value.Value.Jobs);
    }

    private async Task<int> Cancel(JobCommandLine command, CancellationToken ct)
    {
        var route = "/sindri/jobs/" + Uri.EscapeDataString(command.Job ?? string.Empty) + "/cancel";
        var accepted = await client.Post<CancelJobRequest, CommandAccepted>(route, body: null, ct).ConfigureAwait(false);
        return accepted.IsError
            ? await output.Error(accepted.Error).ConfigureAwait(false)
            : await output.Text(string.Create(CultureInfo.InvariantCulture, $"{command.Job} cancel requested (sequence {accepted.Value.Value.Sequence})\n"), 0).ConfigureAwait(false);
    }

    private async Task<int> Resume(JobCommandLine command, CancellationToken ct)
    {
        var route = "/sindri/jobs/" + Uri.EscapeDataString(command.Job ?? string.Empty) + "/resume";
        var resumed = await client.Post<ResumeJobRequest, JobStartedResponse>(route, body: null, ct).ConfigureAwait(false);
        return resumed.IsError
            ? await output.Error(resumed.Error).ConfigureAwait(false)
            : await output.Text(string.Create(CultureInfo.InvariantCulture, $"{command.Job} resumed (sequence {resumed.Value.Value.Accepted.Sequence})\n"), 0).ConfigureAwait(false);
    }
}
