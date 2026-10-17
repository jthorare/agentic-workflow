using Aw.Sindri.Core;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ProbeLaunchTargets;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri launch probe [--detach] [--json]' (API P10a, UI spec E-3): checks the launch targets again. It is a job on the running control plane, so it follows
/// the job as 'services run' does; --detach prints the job id and returns, --json prints the JobStarted body then the final job. Exit codes are the follower's:
/// 0 succeeded, 1 failed, cancelled, left or not running, 2 refused or invalid.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verb writes.</param>
/// <param name="follower">Follows the job the start answers.</param>
internal sealed class LaunchProbeVerb(ControlPlaneClient client, Output output, JobFollower follower)
{
    internal const string Usage = "launch probe [--detach] [--json]";

    internal async Task<int> Run(string[] args, CancellationToken interrupt, CancellationToken again)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (!args.All(static a => string.Equals(a, "--detach", StringComparison.Ordinal) || string.Equals(a, "--json", StringComparison.Ordinal)))
        {
            return await output.Error(SindriErrors.InvalidInput.With($"'launch probe {string.Join(' ', args)}' isn't this command: {Usage}")).ConfigureAwait(false);
        }

        var json = args.Contains("--json", StringComparer.Ordinal);
        var started = await client.Post<ProbeLaunchTargetsRequest, JobStartedResponse>("/sindri/launch/targets/probe", new ProbeLaunchTargetsRequest(), CancellationToken.None).ConfigureAwait(false);   // the start is never cancelled by Ctrl-C: the job may exist on the server
        if (started.IsError)
        {
            return await output.Error(started.Error).ConfigureAwait(false);
        }

        if (!args.Contains("--detach", StringComparer.Ordinal))
        {
            return await follower.Follow(started.Value.Value, json, interrupt, again, describe: null).ConfigureAwait(false);
        }

        return json
            ? await output.Raw(started.Value.Body, 0).ConfigureAwait(false)
            : await output.Text($"job {started.Value.Value.JobId.Value} started\n", 0).ConfigureAwait(false);
    }
}
