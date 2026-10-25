using Aw.Context.Core;
using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;

namespace Aw.Context.Features.GetCode;

/// <summary>
/// The lexical half of the code lens (S-CL): the test files that name an implementing file and the schema files that name a data thing, found by reading the repo at one commit through git (never the
/// working tree) within 'context.code.deadline'. File level: a path and the form that matched leave this class, never a line or text. A repo that can't be read, a commit the clone lacks or an expired
/// deadline is a row of 'searched', never an error of the route.
/// </summary>
internal sealed class CodeLexicalSearch(IRepoReader repos, ICodeSearch search, LexicalTestsRule tests, LexicalSchemaRule schema, TimeProvider clock)
{
    internal async ValueTask<CodeLexicalFindings> Find(CodeLexicalRequest request, CancellationToken cancellationToken)
    {
        using var band = FillBands.Start(FillBands.CodeLexical);
        using var deadline = new CancellationTokenSource(request.Settings.CodeDeadline, clock);   // the TimeProvider's timer, so a test drives it with a fake clock
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var pass = new LexicalPass(request.Settings, request.Map.Graph, request.View.Data.Things, request.Clean);
        try
        {
            await Run(pass, request, limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            pass.Expire();
        }

        return pass.Findings(clock.GetUtcNow());
    }

    private async ValueTask Run(LexicalPass pass, CodeLexicalRequest request, CancellationToken cancellationToken)
    {
        var inScope = ScopeRules.Resolve(request.Settings, request.Scope.Repos, request.Scope.Caller);
        var allowed = inScope.IsError ? [] : inScope.Value;
        var implementing = Implementing(request.View, out var capped);
        pass.Capped = capped;
        var byRepo = Group(pass, request.Map.Graph, implementing, allowed);
        var version = request.Map.Snapshot.Version;
        var targets = allowed.Where(r => byRepo.ContainsKey(r.Name.Value)).ToList();
        if (targets.Count == 0 && allowed.FirstOrDefault(r => r.Name == version.Repo) is { } mapRepo)
        {
            targets.Add(mapRepo);
        }

        foreach (var (repo, index) in targets.Select((r, i) => (r, i)))
        {
            pass.MoreRepos = index < targets.Count - 1;
            var mapCommit = repo.Name == version.Repo && IsSha(version.Commit) ? version.Commit : null;
            await Repo(pass, repo, mapCommit, byRepo.GetValueOrDefault(repo.Name.Value) ?? [], cancellationToken).ConfigureAwait(false);
        }
    }

    // The implementing files by the repo their evidence names; a file no allowed repo claims is counted as unlocated.
    private static Dictionary<string, List<string>> Group(LexicalPass pass, GraphSnapshot graph, List<string> implementing, IReadOnlyList<ContextRepo> allowed)
    {
        var byRepo = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var path in implementing)
        {
            if (RepoOf(graph, path, allowed) is not { } repo)
            {
                pass.Unlocated++;
                continue;
            }

            if (!byRepo.TryGetValue(repo.Name.Value, out var list))
            {
                byRepo[repo.Name.Value] = list = [];
            }

            list.Add(path);
        }

        return byRepo;
    }

    // The files whose tests are searched: confirmed first, then by confidence, then path, at most 50.
    private static List<string> Implementing(CodeView view, out bool capped)
    {
        var ordered = view.Files.OrderByDescending(f => f.Confirmed).ThenByDescending(f => f.Confidence).ThenBy(f => f.Path, StringComparer.Ordinal).ToList();
        capped = ordered.Count > CodeRules.MaxImplementingFiles || view.Truncated.Files;
        return [.. ordered.Take(CodeRules.MaxImplementingFiles).Select(f => f.Path)];
    }

    // The repo a file belongs to: the one its implements edge's evidence names ('repo:<name>/<path>'), when the caller may read it.
    internal static ContextRepo? RepoOf(GraphSnapshot graph, string path, IReadOnlyList<ContextRepo> allowed)
    {
        var key = "code:" + path;
        return graph.Edges.Where(e => e.Type == EdgeType.Implements && e.From.Value == key)
            .SelectMany(e => e.EvidenceOrEmpty)
            .Select(r => allowed.FirstOrDefault(a => r.Key.Value.StartsWith($"repo:{a.Name.Value}/", StringComparison.Ordinal)))
            .FirstOrDefault(r => r is not null);
    }

    private async ValueTask Repo(LexicalPass pass, ContextRepo repo, string? mapCommit, List<string> files, CancellationToken cancellationToken)
    {
        var commit = mapCommit ?? (await repos.Head(repo, cancellationToken).ConfigureAwait(false) is { IsSuccess: true } head ? head.Value : null);
        if (commit is null)
        {
            pass.Fail(ContextErrors.ExtractionUnreadable.Code, commitNotHeld: false, source: null);
            return;
        }

        var resolved = await search.Resolve(repo, commit, cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            // Only a commit the clone really lacks is a recorded limit (110); a checkout git can't read, or anything else, is degraded with its own code.
            pass.Fail(resolved.Error.Code, commitNotHeld: resolved.Error.Code == ContextErrors.CommitUnknown.Code, source: null);
            return;
        }

        commit = resolved.Value;
        var listed = await repos.ListFiles(repo, commit, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            pass.Fail(listed.Error.Code, commitNotHeld: false, source: null);
            return;
        }

        var run = new LexicalRun(pass, repo, commit, [.. listed.Value.Where(p => !repo.Denies(p))]);
        if (files.Count > 0)
        {
            pass.TestsStarted = true;
            pass.Running = LexicalPass.TestsId;
            await tests.Find(run, files, cancellationToken).ConfigureAwait(false);
        }

        pass.SchemaStarted = true;
        pass.Running = LexicalPass.SchemaId;
        await schema.Find(run, cancellationToken).ConfigureAwait(false);
        pass.Running = null;
    }

    internal static bool IsSha(string? commit) => commit is { Length: >= 7 and <= 64 } && commit.All(char.IsAsciiHexDigitLower);
}
