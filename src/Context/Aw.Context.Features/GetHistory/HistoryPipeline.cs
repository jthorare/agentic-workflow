using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.History;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Core.Walks;
using Aw.Kernel;

namespace Aw.Context.Features.GetHistory;

/// <summary>
/// The history lens over one map (S-H): the anchors' own scopes, the stored intent they reach cut by the window, and the landed changes of the files that implement them.
/// Text is not cleaned here (<see cref="HistoryTexts"/> does it), so a backtest arm can hand it an as-of map and read the same cut.
/// </summary>
internal sealed class HistoryPipeline(LensBasisReader bases, IIntentStore intents, IFileHistory files, TimeProvider clock, WalkOptions? walk = null)
{
    private const string Capped = "capped";

    private readonly WalkOptions _walk = walk ?? WalkOptions.Serial;

    internal async ValueTask<Result<HistoryView>> Run(HistoryInputs inputs, IReadOnlyList<string> anchors, ContextSettings settings, ContextScope scope, CancellationToken cancellationToken)
    {
        var resolved = HistoryLens.Resolve(inputs.Map.Graph, anchors, _walk);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var layer = await intents.Layer(cancellationToken).ConfigureAwait(false);
        var repos = ScopeRules.Resolve(settings, scope.Repos, scope.Caller);
        if (layer.IsError || repos.IsError)
        {
            return layer.IsError ? layer.Error : repos.Error;
        }

        var found = resolved.Value;
        var version = inputs.Map.Snapshot.Version;
        var basis = await bases.For(inputs.Map, settings, scope, inputs.Draft, cancellationToken).ConfigureAwait(false);
        var cut = HistoryCut.Apply(Records(found, layer.Value, version.Created), Decisions(found), inputs.Window);
        var git = await Search(found, repos.Value, inputs, settings, cancellationToken).ConfigureAwait(false);
        var changes = HistoryCut.Changes(git.Changes);
        var gitRow = Row(git, basis.AsOf, changes);
        var degraded = git.Degraded;
        if (inputs.Window.Before is { } before && version.Created > before)
        {
            degraded.Add($"map {basis.Map} was written after the 'before' cut ({before.ToString("O", CultureInfo.InvariantCulture)}): its nodes and edges are the map as it stands, not as it was then (fix: ask for an older --map-version)");
        }

        var intentRows = basis.IntentSources.Select(row => Unlinked(row, layer.Value)).ToList();
        var searched = HistoryLens.Searched(found, basis, intentRows, [gitRow, SearchedSources.Incidents], (cut.TotalRecords + cut.TotalDecisions, git.Files));
        var kept = cut.Records.Select(r => r.Key).Concat(cut.Decisions.Select(d => d.Node));
        var counts = new HistoryCounts(cut.TotalRecords, cut.TotalDecisions, changes.Total, git.Files, git.Unlocated, cut.AfterCut);
        var truncated = new HistoryTruncated(cut.RecordsTruncated, cut.DecisionsTruncated, changes.Truncated, git.FilesTruncated);
        var repoOf = NodeRepos.Index(inputs.Map.Graph, basis.MapRepo);
        var records = cut.Records.Select(r => r with { Repo = repoOf.GetValueOrDefault(r.Key.Value) }).ToList();
        var decisions = cut.Decisions.Select(d => d with { Repo = repoOf.GetValueOrDefault(d.Node.Value) }).ToList();
        return new HistoryView(1, found.Anchors, found.MissingAnchors, basis.Map, basis.Draft, basis.AsOf, inputs.Window, records, decisions, changes.Items,
            HistoryIncidents.NotConfigured, HistoryLens.Edges(found, kept), counts, truncated, git.Repos, HistoryLens.Coverage(found, basis, _walk), searched, degraded,
            new Dictionary<string, Citation>(StringComparer.Ordinal));
    }

    private static List<HistoryRecord> Records(HistoryScope found, IntentLayer layer, DateTimeOffset mapCreated)
    {
        var stored = layer.Records.ToDictionary(r => "record:" + r.Key.Value, StringComparer.Ordinal);
        return [.. found.Records.Select(r =>
        {
            var row = stored.GetValueOrDefault(r.Key.Value);
            var times = row is null ? new RecordTimes(CreatedAt: null, UpdatedAt: null) : RecordTimes.Of(row.CreatedAt, row.Version);
            return new HistoryRecord(r.Key, r.Label, r.Source, r.Mentions, r.Trust, r.State, r.Cites, Keys(r.Anchors), times.CreatedAt, times.UpdatedAt, row?.FetchedAt ?? mapCreated, Leaky: false);
        })];
    }

    private static List<HistoryDecision> Decisions(HistoryScope found) =>
        [.. found.Decisions.Select(d => new HistoryDecision(d.Node, d.Text, d.Decides, d.Supersedes, d.Quotes, d.Provenance, d.Confirmed, d.Trust, d.At, d.Quote, Keys(d.Anchors), Leaky: false))];

    private static List<NodeKey> Keys(IReadOnlyList<string>? names) => [.. (names ?? []).Select(NodeKey.From)];

    // An intent source that synced records but linked none of them to any node has nothing to list under an anchor.
    private static SearchedSource Unlinked(SearchedSource row, IntentLayer layer)
    {
        var mine = layer.Records.Where(r => r.Source == row.Id).Select(r => r.Key.Value).ToHashSet(StringComparer.Ordinal);
        return mine.Count > 0 && !layer.Links.Any(l => mine.Contains(l.Record.Value)) ? SearchedSources.Unlinked(row) : row;
    }

    private async ValueTask<GitRead> Search(HistoryScope found, IReadOnlyList<ContextRepo> repos, HistoryInputs inputs, ContextSettings settings, CancellationToken cancellationToken)
    {
        var located = found.Files.Where(f => f.Repo is not null).ToList();
        var unlocated = found.Files.Count - located.Count;
        var mine = located.Where(f => repos.FirstOrDefault(r => r.Name == f.Repo) is { } repo && !repo.Denies(f.Path)).ToList();
        var chosen = mine;   // every implementing file is searched: no cap
        var read = new GitRead(chosen.Count, unlocated, filesTruncated: false);
        using var timer = new CancellationTokenSource(settings.Deadline, clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        var version = inputs.Map.Snapshot.Version;
        var asked = repos.Where(r => chosen.Exists(f => f.Repo == r.Name)).ToList();

        // The repos are read over the cores; their answers are taken in repo order up to the first that ran out of time, as a one-by-one run takes them.
        var answers = await BoundedParallel.Map(asked, (repo, _) => ReadRepo(repo, chosen, version, inputs.Window, (deadline.Token, cancellationToken)), _walk.Degree, cancellationToken).ConfigureAwait(false);
        foreach (var answer in answers)
        {
            if (answer.History is not { } history)
            {
                read.TimedOut = true;
                break;
            }

            read.Add(answer.Repo, answer.Searched, answer.UpTo, history);
        }

        return read;
    }

    // One repo's git read; null history means the deadline ended it (a caller's cancellation is not that: it propagates).
    private async ValueTask<RepoRead> ReadRepo(ContextRepo repo, List<HistoryFile> chosen, MapVersion version, HistoryWindow window, (CancellationToken Deadline, CancellationToken Caller) tokens)
    {
        var mineHere = chosen.Where(f => f.Repo == repo.Name).ToList();
        var upTo = repo.Name == version.Repo ? Commit(version.Commit) : null;
        var query = new FileHistoryQuery(upTo, [.. mineHere.Select(f => f.Path)], window, HistoryRules.ChangesPerRepo);
        try
        {
            return new RepoRead(repo, mineHere, upTo, await files.Changes(repo, query, tokens.Deadline).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (!tokens.Caller.IsCancellationRequested)
        {
            return new RepoRead(repo, mineHere, upTo, History: null);
        }
    }

    private sealed record RepoRead(ContextRepo Repo, List<HistoryFile> Searched, string? UpTo, Result<FileHistory>? History);

    private static string? Commit(string? commit) => commit is { Length: >= 7 } && commit.All(char.IsAsciiHexDigit) ? commit.ToLowerInvariant() : null;

    // Worst first: out of time, then a repo git can't read, then a commit the clone lacks; else ok, with the one limit worth saying.
    private static SearchedSource Row(GitRead git, DateTimeOffset asOf, HistoryChangeList changes)
    {
        var failed = git.Errors.FirstOrDefault(e => e.Code != ContextErrors.CommitUnknown.Code);
        if (git.TimedOut)
        {
            git.Degraded.Add($"git-history: stopped at the deadline; {git.Changes.Count.ToString(CultureInfo.InvariantCulture)} change(s) found so far are listed (SND-CONTEXT-160; fix: raise context.deadline, or narrow the anchors)");
            return SearchedSources.GitHistory(SearchedState.Degraded, reason: null, ContextErrors.HistoryDeadline.Code, asOf);
        }

        if (failed is not null)
        {
            git.Degraded.Add($"git-history: a repo couldn't be read ({failed.Code}; fix: check the checkout)");
            return SearchedSources.GitHistory(SearchedState.Degraded, reason: null, failed.Code, asOf);
        }

        if (git.Errors.Count > 0)
        {
            return SearchedSources.GitHistory(SearchedState.Unavailable, "commit-not-held", ContextErrors.CommitUnknown.Code, asOf);
        }

        var reason = git.Files == 0 ? "no-files" : changes.Truncated || git.Capped || git.FilesTruncated ? Capped : git.Shallow ? "shallow-clone" : null;
        return SearchedSources.GitHistory(SearchedState.Ok, reason, code: null, asOf);
    }

    /// <summary>What the git step found, repo by repo.</summary>
    private sealed class GitRead(int files, int unlocated, bool filesTruncated)
    {
        internal int Files { get; } = files;

        internal int Unlocated { get; } = unlocated;

        internal bool FilesTruncated { get; } = filesTruncated;

        internal bool TimedOut { get; set; }

        internal bool Capped { get; private set; }

        internal bool Shallow { get; private set; }

        internal List<HistoryChange> Changes { get; } = [];

        internal List<HistoryGit> Repos { get; } = [];

        internal List<Error> Errors { get; } = [];

        internal List<string> Degraded { get; } = [];

        internal void Add(ContextRepo repo, List<HistoryFile> searched, string? upTo, Result<FileHistory> history)
        {
            if (history.IsError)
            {
                Errors.Add(history.Error);
                return;
            }

            var read = history.Value;
            Capped |= read.Truncated;
            Shallow |= read.Shallow;
            var changes = read.Changes.Select(c => Change(repo, c, searched)).ToList();
            Changes.AddRange(changes);
            Repos.Add(new HistoryGit(repo.Name, Abbreviate(read.Head), upTo is null ? null : Abbreviate(upTo), read.Shallow, searched.Count, changes.Count));
        }

        private static string Abbreviate(string sha) => sha.Length > 7 ? sha[..7] : sha;

        private static HistoryChange Change(ContextRepo repo, FileChange change, List<HistoryFile> searched)
        {
            var parsed = MergeSubject.Parse(change.Subject);
            var touched = searched.Where(f => change.Files.Contains(f.Path, StringComparer.Ordinal)).ToList();
            return new HistoryChange("commit:" + change.Sha, repo.Name, change.Sha, change.At, change.Subject, change.Parents > 1 ? HistoryChangeKind.Merge : HistoryChangeKind.Commit,
                parsed.Branch, parsed.Pr, change.Files, [.. touched.SelectMany(f => f.Anchors).Distinct(StringComparer.Ordinal).Select(NodeKey.From)], Trust.Untrusted);
        }
    }
}
