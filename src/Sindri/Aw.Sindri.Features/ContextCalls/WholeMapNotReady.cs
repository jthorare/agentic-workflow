using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Results;
using Aw.CodeIndex.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// What the whole map's "no map yet" answer adds to the refusal (UI contract of the empty and building states): the 'building' member while a map build job runs, or while the first
/// code index build of a mapped repo runs and no map job does (#3015; 'stage' says which: 'map' or 'index')
/// (its job, when it started, how many of the engine's sources have synced since it started, how many files of the current repo it has read (its newest "extracting n/N files" line, <see cref="MapBuildFiles"/>), and when the context sources last synced) and the 'noMap' member (the repos, when the profile was approved,
/// when the first build started). Every value is read from state the control plane already keeps (the jobs and profile views, the engine's source state); one that is not
/// known is left out, never written as null. The map builder keeps no node count while it runs, so 'nodesSoFar' is not offered.
/// </summary>
/// <param name="jobs">The 'jobs' view.</param>
/// <param name="views">The views (the profile row).</param>
/// <param name="engine">The context engine (the sources' last syncs).</param>
internal sealed class WholeMapNotReady(IJobViews jobs, IViewReader views, IContextEngine engine)
{
    private static readonly JobKind _build = JobKind.From("BuildContextMap");

    // The first map is built by the context-map-refresh service's job, not a BuildContextMap job (#3595).
    private static readonly JobKind _refresh = JobKind.From("RefreshContextMap");

    // The first map waits on the code index build of its repos (#3015): while one runs and no map job does, the map is building at stage 'index'.
    private static readonly JobKind _index = JobKind.From("BuildCodeIndex");

    /// <summary>The refusal with its extension members.</summary>
    /// <param name="refusal">The engine's SND-CONTEXT-030.</param>
    /// <param name="scope">The caller's scope.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The same error with 'building' (while a build runs) and 'noMap'.</returns>
    internal async ValueTask<Error> Explain(Error refusal, ContextScope scope, CancellationToken cancellationToken)
    {
        var builds = await jobs.List(state: null, _build, cancellationToken).ConfigureAwait(false);
        var refreshes = await jobs.List(state: null, _refresh, cancellationToken).ConfigureAwait(false);
        var ran = (builds.IsSuccess ? builds.Value : []).Concat(refreshes.IsSuccess ? refreshes.Value : []).ToList();
        var running = ran.FirstOrDefault(static j => j.State == JobState.Running) ?? await Indexing(scope, cancellationToken).ConfigureAwait(false);
        var profile = await views.Get<ProfileRow>(ViewNames.Profile, ViewNames.Profile, cancellationToken).ConfigureAwait(false);
        var explained = refusal;
        if (running is not null)
        {
            var synced = await engine.Sources(scope, cancellationToken).ConfigureAwait(false);
            explained = explained.WithExtension("building", Building(running, synced.IsSuccess ? synced.Value.Sources : null));
        }

        DateTimeOffset? first = ran.Count == 0 ? null : ran.Min(static j => j.StartedAt);
        return explained.WithExtension("noMap", NoMap(scope, profile.IsSuccess ? profile.Value?.ApprovedAt : null, first));
    }

    /// <summary>The 'building' member for the running build <paramref name="job"/>: 'stage' is 'index' for a code index build and 'map' for a map build, and the files are that build's own count.</summary>
    /// <param name="job">The running build job (a BuildContextMap or a RefreshContextMap job, or the BuildCodeIndex job of a mapped repo).</param>
    /// <param name="sources">The engine's sources, or null when they could not be read.</param>
    /// <returns>The member: only what is known.</returns>
    internal static JsonElement Building(JobRow job, IReadOnlyList<SourceStatusRow>? sources) => Write(w =>
    {
        w.WriteStartObject();
        var index = job.Kind.Equals(_index);
        w.WriteString("stage", index ? "index" : "map");
        var lastSync = sources?.Max(static s => s.LastSync);
        if (sources is { Count: > 0 })
        {
            // A source counts as read once it synced after this build started.
            w.WriteNumber("sourcesDone", sources.Count(s => s.LastSync >= job.StartedAt));
            w.WriteNumber("sourcesTotal", sources.Count);
        }

        if (Files(job, index) is var (done, total))
        {
            w.WriteNumber("filesDone", done);
            w.WriteNumber("filesTotal", total);
        }

        w.WriteString("startedAt", Time(job.StartedAt));
        if (lastSync is { } synced)
        {
            w.WriteString("lastSyncAt", Time(synced));
        }

        w.WriteString("jobId", job.Id.Value);
        w.WriteEndObject();
    });

    // The running code index build of a repo the map covers, or null (a failed jobs read is no build: the answer stays "no map yet").
    private async ValueTask<JobRow?> Indexing(ContextScope scope, CancellationToken cancellationToken)
    {
        var indexes = await jobs.List(JobState.Running, _index, cancellationToken).ConfigureAwait(false);
        return indexes.IsError ? null : indexes.Value.FirstOrDefault(j => j.State == JobState.Running && scope.Repos.Any(r => IndexBuildWords.IsBuildOf(j, r.Name.Value)));
    }

    // The build's own files line: "structure n of N files" for a code index build, "extracting n/N files" for a map build.
    private static (int Done, int Total)? Files(JobRow job, bool index)
    {
        var lines = job.Progress.Select(static l => l.Line);
        return index
            ? IndexBuildFiles.Newest(lines) is { } indexed ? (indexed.Done, indexed.Total) : null
            : MapBuildFiles.Newest(lines) is { } mapped ? (mapped.Done, mapped.Total) : null;
    }

    private static JsonElement NoMap(ContextScope scope, DateTimeOffset? approvedAt, DateTimeOffset? firstRunStartedAt) => Write(w =>
    {
        w.WriteStartObject();
        w.WriteStartArray("repos");
        foreach (var repo in scope.Repos)
        {
            w.WriteStringValue(repo.Name.Value);
        }

        w.WriteEndArray();
        if (approvedAt is { } approved)
        {
            w.WriteString("approvedAt", Time(approved));
        }

        if (firstRunStartedAt is { } first)
        {
            w.WriteString("firstRunStartedAt", Time(first));
        }

        w.WriteEndObject();
    });

    private static string Time(DateTimeOffset value) => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static JsonElement Write(Action<Utf8JsonWriter> body)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            body(writer);
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }
}
