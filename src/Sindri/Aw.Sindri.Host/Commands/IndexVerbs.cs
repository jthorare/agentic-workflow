using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Features.GetJob;
using Aw.Sindri.Host.Text;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri index sync [source] | status [--json] | purge &lt;source&gt;' (owned-index spec §5.5, §5.8, §10): clients of the control plane. <b>sync</b> without a source runs the 'index-sync' service as a job and follows it as
/// 'services run' does (it ticks every feed). With a source it checks the name against the feeds, then starts that source's own job (POST /sindri/index/sync): only that feed ticks, the scheduled
/// run being in flight does not refuse it, and a source that ended with a code (down, capped, already syncing) exits 1. <b>status</b> reads GET /sindri/index/status and never starts the control plane.
/// <b>purge</b> is a human's verb: it asks for the counts (the preview is refused to a session, SND-AUTH-002), shows them and deletes only after a typed yes at a terminal.
/// Exit codes are the context verbs': 0 ok, 1 attention (a source ended with a code, not running, refused, a purge not confirmed), 2 a usage or tool error.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="follower">Follows the job a sync starts.</param>
/// <param name="terminal">Whether standard input and output are a terminal (purge asks its typed yes only there).</param>
/// <param name="input">Where the typed yes is read.</param>
/// <param name="ensureRunning">Starts the control plane for sync (true: this verb may launch it) or names why not; null: never start it.</param>
internal sealed class IndexVerbs(ControlPlaneClient client, Output output, JobFollower follower, bool terminal, TextReader input, Func<bool, CancellationToken, Task<Error?>>? ensureRunning = null)
{
    internal const string Usage = "index sync [<source>] [--detach] [--json] [--no-start] | index status [--json] | index purge <source>";

    private const string StatusRoute = "/sindri/index/status";
    private const string SyncRoute = "/sindri/index/sync";
    private const string PurgeRoute = "/sindri/index/purge";

    private static readonly string[] _syncFlags = ["--detach", "--json", "--no-start"];

    internal async Task<int> Run(string[] args, CancellationToken interrupt, CancellationToken again)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args switch
        {
            ["status", .. var rest] when rest is [] or ["--json"] => await Status(rest.Length == 1, interrupt).ConfigureAwait(false),
            ["sync", .. var rest] when Sync(rest) is { } line => await RunSync(line, interrupt, again).ConfigureAwait(false),
            ["purge", var source] when !source.StartsWith("--", StringComparison.Ordinal) => await Purge(source, interrupt).ConfigureAwait(false),
            _ => await output.Error(SindriErrors.InvalidInput.With($"'index {string.Join(' ', args)}' isn't an index command: {Usage}")).ConfigureAwait(false),
        };
    }

    private async Task<int> Status(bool json, CancellationToken ct)
    {
        var status = await client.Get<IndexStatusView>(StatusRoute, ct).ConfigureAwait(false);
        if (status.IsError)
        {
            return await Failed(status.Error).ConfigureAwait(false);
        }

        var exit = status.Value.Value.Sources.Any(static s => s.LastCode is not null) ? 1 : 0;
        return json ? await output.Raw(status.Value.Body, exit).ConfigureAwait(false) : await output.Text(IndexText.Status(status.Value.Value), exit).ConfigureAwait(false);
    }

    private async Task<int> RunSync(SyncLine line, CancellationToken interrupt, CancellationToken again)
    {
        if (line.Source is not { } named)
        {
            return await new ServiceVerbs(client, output, follower, ensureRunning).Run(["run", "index-sync", .. line.Flags], interrupt, again).ConfigureAwait(false);
        }

        var refused = await CheckFeed(named, line.Flags.Contains("--no-start", StringComparer.Ordinal), interrupt).ConfigureAwait(false);
        if (refused is { } code)
        {
            return code;
        }

        var started = await client.Post<SyncIndexSourceRequest, JobStartedResponse>(SyncRoute, new SyncIndexSourceRequest(named), interrupt).ConfigureAwait(false);
        if (started.IsError)
        {
            return await Failed(started.Error).ConfigureAwait(false);
        }

        var json = line.Flags.Contains("--json", StringComparer.Ordinal);
        if (line.Flags.Contains("--detach", StringComparer.Ordinal))
        {
            return json
                ? await output.Raw(started.Value.Body, 0).ConfigureAwait(false)
                : await output.Text($"job {started.Value.Value.JobId.Value} started\n", 0).ConfigureAwait(false);
        }

        var exit = await follower.Follow(started.Value.Value, json, interrupt, again).ConfigureAwait(false);
        return exit == 0 && await EndedWithCode(started.Value.Value, interrupt).ConfigureAwait(false) ? 1 : exit;
    }

    /// <summary>A source that is down, capped or already syncing is a row with a code, not a failed job; the job ended well, so its result says whether the source did.</summary>
    private async Task<bool> EndedWithCode(JobStartedResponse started, CancellationToken ct)
    {
        var job = await client.Get<JobView>(started.Links.Job, ct).ConfigureAwait(false);
        if (job.IsError || job.Value.Value.Result is not { } result)
        {
            return false;
        }

        var read = output.Read<SyncIndexSourceResult>(Encoding.UTF8.GetBytes(result.GetRawText()));
        return read.IsSuccess && read.Value.Code is not null;
    }

    /// <summary>A name has to be one of the feeds: a typo is caught before a job starts. Null when it is; else the exit code.</summary>
    private async Task<int?> CheckFeed(string named, bool noStart, CancellationToken ct)
    {
        var down = ensureRunning is null ? null : await ensureRunning(!noStart, ct).ConfigureAwait(false);
        if (down is not null)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        var status = await client.Get<IndexStatusView>(StatusRoute, ct).ConfigureAwait(false);
        if (status.IsError)
        {
            return await Failed(status.Error).ConfigureAwait(false);
        }

        var feeds = status.Value.Value.Sources.Select(static s => s.Source).ToList();
        return feeds.Contains(named, StringComparer.Ordinal)
            ? null
            : await output.Error(SindriErrors.InvalidInput.With($"'{named}' isn't a record source of this profile (sources: {(feeds.Count == 0 ? "none" : string.Join(", ", feeds))})")).ConfigureAwait(false);
    }

    private async Task<int> Purge(string source, CancellationToken ct)
    {
        if (!terminal)
        {
            return await output.Error(SindriErrors.InvalidInput.With("index purge asks for a typed yes, which needs a terminal (fix: run it in one)")).ConfigureAwait(false);
        }

        var preview = await client.Post<PurgeIndexSourceRequest, IndexPurgeView>(PurgeRoute, new PurgeIndexSourceRequest(source, Confirm: false), ct).ConfigureAwait(false);
        if (preview.IsError)
        {
            return await Failed(preview.Error).ConfigureAwait(false);
        }

        var held = preview.Value.Value;
        await output.Out.WriteAsync($"{held.Source}: deletes {IndexText.Counts(held)}. A later sync refills only what {held.Source} still has, not what it has since deleted.\n").ConfigureAwait(false);
        await output.OnStandardError().Out.WriteAsync("Type yes to delete: ").ConfigureAwait(false);
        var typed = (await input.ReadLineAsync(ct).ConfigureAwait(false))?.Trim();
        if (!string.Equals(typed, "yes", StringComparison.Ordinal))
        {
            return await output.Text("nothing was deleted\n", 1).ConfigureAwait(false);
        }

        var done = await client.Post<PurgeIndexSourceRequest, IndexPurgeView>(PurgeRoute, new PurgeIndexSourceRequest(source, Confirm: true), ct).ConfigureAwait(false);
        return done.IsError
            ? await Failed(done.Error).ConfigureAwait(false)
            : await output.Text($"purged {done.Value.Value.Source}: {IndexText.Counts(done.Value.Value)}\n", 0).ConfigureAwait(false);
    }

    private async Task<int> Failed(Error error) => ContextExit.For(error, await output.Error(error).ConfigureAwait(false));

    private sealed record SyncLine(string? Source, string[] Flags);

    /// <summary>The optional source and the flags of 'index sync'; null for a second word or a flag it doesn't take.</summary>
    private static SyncLine? Sync(string[] rest)
    {
        var flags = rest.Where(static a => a.StartsWith("--", StringComparison.Ordinal)).ToArray();
        var words = rest.Where(static a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();
        return words.Count <= 1 && flags.All(static f => _syncFlags.Contains(f, StringComparer.Ordinal)) ? new SyncLine(words.Count == 1 ? words[0] : null, flags) : null;
    }
}
