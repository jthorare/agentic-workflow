using System.Text;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Features.GetJob;
using Aw.Sindri.Host.Text;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'context sources sync [&lt;source&gt;]' and 'context map build' (API P6 follow-up): each starts its job and follows it through the
/// <see cref="JobFollower"/> as 'services run' does (Ctrl-C cancels the job, a second Ctrl-C leaves); --detach prints the job id and returns, --json
/// prints the JobStarted body then the final job. Exit codes are the follower's: 0 succeeded, 1 failed, cancelled, left or not running, 2 refused or invalid.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="follower">Follows the job a start answers.</param>
internal sealed class ContextJobVerbs(ControlPlaneClient client, Output output, JobFollower follower)
{
    internal const string SyncUsage = "context sources sync [<source>] [--detach] [--json]";
    internal const string BacktestUsage = "context eval backtest [--arms a,b|all] [--since <date>] [--until <date>] [--limit <n>] [--detach] [--json]";
    internal const string BuildUsage = "context map build [--detach] [--json]";
    internal const string RefreshUsage = "context map refresh [--detach] [--json]";

    internal async Task<int> Run(string[] args, CancellationToken interrupt, CancellationToken again)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args is ["eval", "backtest", ..])
        {
            return await Backtest(args[2..], interrupt, again).ConfigureAwait(false);
        }

        var sync = args is ["sources", ..];
        var refresh = args is ["map", "refresh", ..];
        var line = Parse(args[2..], sync ? 1 : 0);
        if (line is null)
        {
            return await output.Error(SindriErrors.InvalidInput.With($"'context {string.Join(' ', args)}' isn't this command: {(sync ? SyncUsage : UsageOf(refresh))}")).ConfigureAwait(false);
        }

        if (refresh)
        {
            return await Refresh((line.Detach, line.Json), interrupt, again).ConfigureAwait(false);
        }

        // Ctrl-C ends the start POST (as 'services run'; nothing else bounds a hung start): the job may already exist on the server, which `sindri jobs` shows
        var started = sync
            ? await client.Post<SyncContextSourcesRequest, JobStartedResponse>("/sindri/context/sources/sync", new SyncContextSourcesRequest(line.Words.Count == 1 ? line.Words[0] : null), interrupt).ConfigureAwait(false)
            : await client.Post<BuildContextMapRequest, JobStartedResponse>("/sindri/context/map/build", new BuildContextMapRequest(), interrupt).ConfigureAwait(false);
        if (started.IsError)
        {
            return await output.Error(started.Error).ConfigureAwait(false);
        }

        return await Followed(started.Value, (line.Detach, line.Json), interrupt, again, sync ? null : BuildTable).ConfigureAwait(false);
    }

    private static string UsageOf(bool refresh) => refresh ? RefreshUsage : BuildUsage;

    /// <summary>'context map refresh [--detach] [--json]': the refresh job (owner F1).</summary>
    private async Task<int> Refresh((bool Detach, bool Json) flags, CancellationToken interrupt, CancellationToken again)
    {
        var begun = await client.Post<RefreshContextMapRequest, JobStartedResponse>("/sindri/context/map/refresh", new RefreshContextMapRequest(), interrupt).ConfigureAwait(false);
        return begun.IsError
            ? await output.Error(begun.Error).ConfigureAwait(false)
            : await Followed(begun.Value, flags, interrupt, again, RefreshTable).ConfigureAwait(false);
    }

    /// <summary>'context eval backtest [--arms a,b|all] [--since D] [--until D] [--limit N] [--detach] [--json]': the backtest job (context spec §9.1).</summary>
    private async Task<int> Backtest(string[] rest, CancellationToken interrupt, CancellationToken again)
    {
        var line = BacktestLine.Parse(rest);
        if (line is null)
        {
            return await output.Error(SindriErrors.InvalidInput.With($"'context eval backtest {string.Join(' ', rest)}' isn't this command: {BacktestUsage}")).ConfigureAwait(false);
        }

        var started = await client.Post<RunContextBacktestRequest, JobStartedResponse>("/sindri/context/eval/backtest", line.Request, interrupt).ConfigureAwait(false);
        return started.IsError
            ? ContextExit.For(started.Error, await output.Error(started.Error).ConfigureAwait(false))   // 082 (an unknown arm) is attention: exit 1, as the context error table says
            : await Followed(started.Value, (line.Detach, line.Json), interrupt, again, Table).ConfigureAwait(false);
    }

    /// <summary>The build's draft line and its degraded reasons, read from the final job's small result; nothing when the result isn't there or doesn't read.</summary>
    private string? BuildTable(JobView job)
    {
        if (job.Result is not { } result)
        {
            return null;
        }

        var read = output.Read<BuildContextMapResult>(Encoding.UTF8.GetBytes(result.GetRawText()));
        return read.IsSuccess ? MapBuildJobText.Render(read.Value) : null;
    }

    /// <summary>The refresh's one-line outcome, read from the final job's small result; nothing when the result isn't there or doesn't read.</summary>
    private string? RefreshTable(JobView job)
    {
        if (job.Result is not { } result)
        {
            return null;
        }

        var read = output.Read<RefreshContextMapResult>(Encoding.UTF8.GetBytes(result.GetRawText()));
        return read.IsSuccess ? MapRefreshJobText.Render(read.Value) : null;
    }

    /// <summary>The backtest's rows, read from the final job's small result; nothing when the result isn't there or doesn't read.</summary>
    private string? Table(JobView job)
    {
        if (job.Result is not { } result)
        {
            return null;
        }

        var read = output.Read<RunContextBacktestResult>(Encoding.UTF8.GetBytes(result.GetRawText()));
        return read.IsSuccess ? BacktestJobText.Render(read.Value) : null;
    }

    private async Task<int> Followed(ApiAnswer<JobStartedResponse> started, (bool Detach, bool Json) mode, CancellationToken interrupt, CancellationToken again, Func<JobView, string?>? describe = null)
    {
        var (detach, json) = mode;
        if (!detach)
        {
            return await follower.Follow(started.Value, json, interrupt, again, describe).ConfigureAwait(false);
        }

        return json
            ? await output.Raw(started.Body, 0).ConfigureAwait(false)
            : await output.Text($"job {started.Value.JobId.Value} started\n", 0).ConfigureAwait(false);
    }

    /// <summary>The words (at most <paramref name="maxWords"/>) and the --detach and --json flags; null for anything else.</summary>
    private static ContextJobLine? Parse(string[] rest, int maxWords)
    {
        var flags = rest.Where(static a => a.StartsWith("--", StringComparison.Ordinal)).ToList();
        var words = rest.Where(static a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();
        var known = flags.TrueForAll(static f => string.Equals(f, "--detach", StringComparison.Ordinal) || string.Equals(f, "--json", StringComparison.Ordinal));
        return known && words.Count <= maxWords ? new ContextJobLine(words, flags.Contains("--detach", StringComparer.Ordinal), flags.Contains("--json", StringComparer.Ordinal)) : null;
    }

    private sealed record ContextJobLine(IReadOnlyList<string> Words, bool Detach, bool Json);
}
