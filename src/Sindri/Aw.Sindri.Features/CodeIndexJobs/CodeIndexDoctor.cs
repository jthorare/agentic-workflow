using System.Globalization;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using IndexRepoHead = Aw.CodeIndex.Core.Model.RepoHead;
using IndexRepoStatus = Aw.CodeIndex.Core.Facade.RepoStatus;

namespace Aw.Sindri.Features.CodeIndexJobs;

/// <summary>
/// The code index's doctor rows (code index spec §8): 'code-index:&lt;repo&gt;' per repo (ok, warn when stale, first build queued, waiting or retrying, fail when damaged) and 'code-index:graphify',
/// 'code-index:ollama' and 'code-index:lock', read from the layer statuses the last builds stored, so doctor runs no tool. A layer that is unavailable is a warning, never a failure.
/// </summary>
/// <param name="index">The code index: its status, from the files only.</param>
/// <param name="heads">The repos' live heads.</param>
/// <param name="jobs">The 'jobs' view.</param>
/// <param name="probes">The live checks: the embedding server and model, and the lock file.</param>
/// <param name="clock">Dates the retry words.</param>
/// <param name="liveProfile">The live profile file: its repos the profile doesn't name are checked too (owner q102, CIX-L59).</param>
/// <param name="pauses">The paused services: no build is coming while code-index-main-move is paused (#3399).</param>
internal sealed class CodeIndexDoctor(ICodeIndex index, IRepoHeads heads, IJobViews jobs, CodeIndexProbes probes, TimeProvider clock, IProfileSource liveProfile, IServicePauses pauses) : ICodeIndexChecks
{
    private const string GraphCheck = "code-index:graphify";
    private const string OllamaCheck = "code-index:ollama";
    private const string LockCheck = "code-index:lock";
    private const string Setup = "./setup.sh";

    /// <summary>The checks, in the order doctor prints them.</summary>
    /// <param name="approved">The profile, or null.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The repo rows, then graphify, ollama and the lock.</returns>
    public async ValueTask<DoctorCheck[]> Check(LoadedProfile? approved, CancellationToken cancellationToken)
    {
        if (approved is null)
        {
            return [new DoctorCheck("code-index", CheckState.Ok, "not configured: no profile", Fix: null)];
        }

        var file = await liveProfile.Load(cancellationToken).ConfigureAwait(false);
        var scope = CodeIndexJobScopes.For(approved.Profile, file.IsError ? null : file.Value.Profile, caller: null);
        if (scope.IsError)
        {
            return [new DoctorCheck("code-index", CheckState.Fail, $"{scope.Error.Code}: {scope.Error.Message}", "edit the profile, then sindri profile validate")];
        }

        var status = await index.Status(scope.Value, cancellationToken).ConfigureAwait(false);
        if (status.IsError)
        {
            return [new DoctorCheck("code-index", CheckState.Fail, $"{status.Error.Code}: {status.Error.Message}", "check $AW_STATE_DIR/code-index")];
        }

        var live = await heads.Heads(scope.Value.Repos, cancellationToken).ConfigureAwait(false);
        var builds = await jobs.List(state: null, IndexJobs.BuildKind, cancellationToken).ConfigureAwait(false);
        var embeddings = scope.Value.Repos.Select(static r => r.Settings.Embeddings).FirstOrDefault();
        var found = await probes.Check(embeddings, cancellationToken).ConfigureAwait(false);
        var active = await MainMovePause.Active(pauses, cancellationToken).ConfigureAwait(false);
        return Rows(new IndexReads(status.Value.Repos, live.IsError ? [] : live.Value, builds.IsError ? [] : builds.Value, active), clock.GetUtcNow(), found);
    }

    /// <summary>What the live checks found: the embedding server and model, and the lock file.</summary>
    /// <param name="Model">The embeddings probe.</param>
    /// <param name="Lock">The lock file check.</param>
    internal sealed record Probes(ModelProbe Model, LockState Lock);

    /// <summary>The embeddings probe: whether the layer is on, and the answer when it is.</summary>
    /// <param name="Enabled">False when the profile turns embeddings off.</param>
    /// <param name="Result">The probe's answer.</param>
    internal sealed record ModelProbe(bool Enabled, Result<Unit> Result);

    /// <summary>What the doctor read from the stored index, the heads, the jobs and the services.</summary>
    /// <param name="Repos">Each repo's index status.</param>
    /// <param name="Live">The repos' live heads (empty when unreadable).</param>
    /// <param name="Builds">The build jobs of every repo.</param>
    /// <param name="BuildServiceActive">Whether code-index-main-move runs; false means no build is coming (#3399).</param>
    internal sealed record IndexReads(IReadOnlyList<IndexRepoStatus> Repos, IReadOnlyList<IndexRepoHead> Live, IReadOnlyList<JobRow> Builds, bool BuildServiceActive = true);

    /// <summary>The rows for what was read; pure, so the words are tested without a box.</summary>
    /// <param name="reads">What was read.</param>
    /// <param name="now">The time.</param>
    /// <param name="probes">The live checks, or null when none was made (the ollama and lock rows then read the stored layers only).</param>
    /// <returns>The rows.</returns>
    internal static DoctorCheck[] Rows(IndexReads reads, DateTimeOffset now, Probes? probes = null)
    {
        ArgumentNullException.ThrowIfNull(reads);
        var (repos, live, builds, buildServiceActive) = reads;
        _ = buildServiceActive;
        ArgumentNullException.ThrowIfNull(repos);
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(builds);
        if (repos.Count == 0)
        {
            return [new DoctorCheck("code-index", CheckState.Ok, "no repos in the profile", Fix: null)];
        }

        var layers = repos.SelectMany(static r => r.Layers).ToList();
        return
        [
            .. repos.Select(r => Repo(r, live.FirstOrDefault(h => h.Repo == r.Repo), [.. builds.Where(j => IndexJobs.IsOf(j, r.Repo.Value))], now)),
            Layer(GraphCheck, "graph", layers, LayerName.Graph),
            probes is null ? Layer(OllamaCheck, "embeddings", layers, LayerName.Embeddings) : Ollama(probes.Model),
            probes is null ? new DoctorCheck(LockCheck, CheckState.Ok, "lock not checked", Fix: null) : Lock(probes.Lock),
        ];
    }

    private static DoctorCheck Repo(IndexRepoStatus repo, IndexRepoHead? live, List<JobRow> mine, DateTimeOffset now)
    {
        var name = "code-index:" + repo.Repo.Value;
        var running = mine.Exists(static j => j.State == JobState.Running);
        var finished = mine.Where(static j => j.State != JobState.Running && j.EndedAt is not null).OrderByDescending(static j => j.EndedAt).ToList();
        var buildWords = running ? "building" : IndexBuildWords.Waiting(finished, now) ?? "build queued";
        return repo.State switch
        {
            IndexState.Damaged => new DoctorCheck(name, CheckState.Fail, "index damaged, rebuilding (SND-CODEIDX-010)", "none needed: the next tick deletes and rebuilds it; sindri jobs shows the build"),
            IndexState.Missing => new DoctorCheck(name, CheckState.Warn, running ? "building the first index" : IndexBuildWords.Waiting(finished, now) ?? "first build queued", Fix: null),
            IndexState.Ok or _ => Readable(name, repo, live, buildWords, now),
        };
    }

    private static DoctorCheck Readable(string name, IndexRepoStatus repo, IndexRepoHead? live, string buildWords, DateTimeOffset now)
    {
        var commit = repo.Commit?.Value ?? string.Empty;
        var short7 = commit.Length >= 7 ? commit[..7] : commit;
        if (live?.OnDefaultBranch == false)
        {
            return new DoctorCheck(name, CheckState.Warn, $"checkout is not on main; the index at {short7} is served", "switch the repo's checkout to its default branch");
        }

        if (live?.Head is { } head && head != repo.Commit)
        {
            return new DoctorCheck(name, CheckState.Warn, $"stale: index at {short7}, head at {head.Value[..7]}; {buildWords}", "none needed: the next tick builds it");
        }

        return Down(name, short7, repo.Layers) ?? new DoctorCheck(name, CheckState.Ok, $"{short7}, {Age(repo.BuiltAt, now)}, {string.Join(" · ", repo.Layers.Select(static l => $"{Word(l.Layer)} {Word(l.State)}"))}", Fix: null);
    }

    // A layer down for a named cause is a warning on the repo's own row (gh-3069): the live probe rows can be ok while this index still lacks the layer.
    private static DoctorCheck? Down(string name, string short7, IReadOnlyList<LayerStatus> layers) =>
        layers.FirstOrDefault(static l => l.State == LayerState.Unavailable && l.Code is not null) is { } down
            ? new DoctorCheck(name, CheckState.Warn, $"{short7}: {Word(down.Layer)} unavailable ({down.Code}): {down.Detail}", Fix(down.Code))
            : null;

    private static DoctorCheck Layer(string name, string word, List<LayerStatus> layers, LayerName which)
    {
        var mine = layers.Where(l => l.Layer == which).ToList();
        if (mine.Find(static l => l.State == LayerState.Unavailable) is { } down)
        {
            return new DoctorCheck(name, CheckState.Warn, $"{word} unavailable ({down.Code}): {down.Detail}", Fix(down.Code));
        }

        return new DoctorCheck(name, CheckState.Ok, mine.Count == 0 ? "no index built yet" : $"{word} {Word(mine[0].State)}", Fix: null);
    }

    // A live check, and a warning whatever it finds: embeddings are an extra, never a failure.
    private static DoctorCheck Ollama(ModelProbe probe) => !probe.Enabled
        ? new DoctorCheck(OllamaCheck, CheckState.Warn, "embeddings are off in the profile", "set context.codeIndex.embeddings.enabled: true to turn them on")
        : probe.Result.IsSuccess
            ? new DoctorCheck(OllamaCheck, CheckState.Ok, "Ollama is reachable and the model matches the pin", Fix: null)
            : new DoctorCheck(OllamaCheck, CheckState.Warn, $"embeddings unavailable ({probe.Result.Error.Code}): {probe.Result.Error.Message}", Fix(probe.Result.Error.Code));

    private static DoctorCheck Lock(LockState state) => state switch
    {
        LockState.Matches => new DoctorCheck(LockCheck, CheckState.Ok, "context/code-index.lock parses and matches the pins this build carries", Fix: null),
        LockState.Mismatch => new DoctorCheck(LockCheck, CheckState.Warn, "context/code-index.lock does not match the pins this build carries", Setup + " re-installs the pin"),
        LockState.Unparsable => new DoctorCheck(LockCheck, CheckState.Warn, "context/code-index.lock does not parse", "restore it from the toolkit's checkout (scripts/code-index-lock.sh check)"),
        LockState.Missing or _ => new DoctorCheck(LockCheck, CheckState.Warn, "context/code-index.lock was not found", "set AW_CODE_INDEX_LOCK to the file in the toolkit's checkout"),
    };

    private static string? Fix(string? code) => code switch
    {
        "SND-CODEIDX-007" => "install bubblewrap (" + Setup + "), or set index.graph.enabled: false",
        "SND-CODEIDX-006" => "start Ollama (the pin is in context/code-index.lock)",
        _ => Setup + " installs the pin",
    };

    private static string Age(DateTimeOffset? builtAt, DateTimeOffset now)
    {
        if (builtAt is not { } at)
        {
            return "age unknown";
        }

        var age = now - at;
        return age.TotalMinutes < 1 ? "just built"
            : age.TotalHours < 1 ? $"{((int)age.TotalMinutes).ToString(CultureInfo.InvariantCulture)} min old"
            : age.TotalDays < 1 ? $"{((int)age.TotalHours).ToString(CultureInfo.InvariantCulture)} h old"
            : $"{((int)age.TotalDays).ToString(CultureInfo.InvariantCulture)} d old";
    }

    private static string Word(LayerName layer) => layer.ToString().ToLowerInvariant();

    private static string Word(LayerState state) => state.ToString().ToLowerInvariant();
}
