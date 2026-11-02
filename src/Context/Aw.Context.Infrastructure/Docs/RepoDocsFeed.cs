using System.Security.Cryptography;
using System.Text;
using Aw.Context.Core.Audience;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Context.Infrastructure.Git;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Docs;

/// <summary>
/// The repo-docs record feed (spec §5.2, U7): per repo the documents the globs match at its head, read from git, each a
/// 'doc' record keyed 'repo:&lt;repo&gt;/&lt;path&gt;' whose version is the blob id. A path under the repo's deny globs, or one the ids
/// can't carry, is never a record. The text is raw: the sync driver cleans and chunks it (<see cref="RecordBatches"/>), so
/// a blocked document is decided there. A stored record with no document at the head is in <see cref="RecordPage.Removed"/>.
/// The cursor is a fingerprint of the scrub ruleset and every repo's head, so a pull at the same heads is an empty page
/// (a replay writes nothing), a new head or ruleset is diffed against the store, and a page cut short by the limit answers
/// a cursor that is not final, so the next pull carries on with what the store doesn't hold yet.
/// </summary>
internal sealed class RepoDocsFeed(IReadOnlyList<ContextRepo> repos, IReadOnlyList<string> globs, IReadOnlyList<string> githubRepos, IRecordStore store, GitCli git, Scrubbers scrubbers)
    : RecordFeed(RepoDocs.Id)
{
    private const int ListPage = 1000;
    private const string Partial = "more:";

    private readonly Glob _globs = new(globs);

    public override async ValueTask<Result<RecordPage>> Pull(RecordPull pull, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pull);
        var heads = new List<RepoHead>();
        foreach (var repo in repos.OrderBy(r => r.Name.Value, StringComparer.Ordinal))
        {
            var head = await Head(repo, cancellationToken).ConfigureAwait(false);
            if (head.IsError)
            {
                return head.Error;
            }

            heads.Add(head.Value);
        }

        var final = Fingerprint(heads);
        if (pull.After is { } after && string.Equals(after.Value, final, StringComparison.Ordinal))
        {
            return new RecordPage([], [], after, CursorReset: false);
        }

        var page = await Collect(heads, Math.Max(pull.Limit, 1), cancellationToken).ConfigureAwait(false);
        return page.Map(p => new RecordPage(p.Items, p.Removed, SourceCursor.From(p.Complete ? final : Partial + final), CursorReset: false));
    }

    /// <summary>Diffs every repo against the store and reads up to <paramref name="limit"/> changed documents; not complete when some were left for the next pull.</summary>
    private async ValueTask<Result<(List<RecordItem> Items, List<RecordKey> Removed, bool Complete)>> Collect(List<RepoHead> heads, int limit, CancellationToken cancellationToken)
    {
        var items = new List<RecordItem>();
        var removed = new List<RecordKey>();
        var complete = true;
        foreach (var head in heads)
        {
            var plan = await Plan(head, cancellationToken).ConfigureAwait(false);
            if (plan.IsError)
            {
                return plan.Error;
            }

            removed.AddRange(plan.Value.Removed);
            foreach (var (path, blob) in plan.Value.Changed)
            {
                var item = items.Count < limit ? await Item(head.Repo, path, blob, cancellationToken).ConfigureAwait(false) : null;
                if (item is null)
                {
                    complete = false;   // over the limit, or git can't read the blob this run: the next pull carries on
                    continue;
                }

                items.Add(item);
            }
        }

        return (items, removed, complete);
    }

    private async ValueTask<Result<RepoHead>> Head(ContextRepo repo, CancellationToken cancellationToken)
    {
        var head = await git.Head(repo.Path, cancellationToken).ConfigureAwait(false);
        var tree = head.IsError ? head.Error : await git.Tree(repo.Path, head.Value, cancellationToken).ConfigureAwait(false);
        return tree.IsError ? tree.Error : new RepoHead(repo, head.Value, tree.Value);
    }

    private string Fingerprint(List<RepoHead> heads) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            scrubbers.Ruleset.Value + string.Concat(heads.Select(h => $"|{h.Repo.Name.Value}={h.Head}")))));

    /// <summary>Diffs the repo's head against the store: documents new, changed or cleaned under another ruleset, and records whose document is gone.</summary>
    private async ValueTask<Result<RepoDocsPlan>> Plan(RepoHead head, CancellationToken cancellationToken)
    {
        var wanted = head.Tree.Where(e => _globs.Matches(e.Key) && Citable(head.Repo, e.Key)).ToDictionary(StringComparer.Ordinal);
        var stored = await Stored(head.Repo, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        var ruleset = scrubbers.Ruleset.Value;
        var changed = wanted.Where(w => !(stored.Value.TryGetValue(RepoDocs.Key(head.Repo.Name, w.Key), out var row)
                && row.Version.Value == w.Value && string.Equals(row.Ruleset, ruleset, StringComparison.Ordinal)))
            .OrderBy(w => w.Key, StringComparer.Ordinal).ToList();
        var live = wanted.Keys.Select(path => RepoDocs.Key(head.Repo.Name, path)).ToHashSet();
        var removed = stored.Value.Keys.Where(k => !live.Contains(k)).OrderBy(k => k.Value, StringComparer.Ordinal).ToList();
        return new RepoDocsPlan(changed, removed);
    }

    private async ValueTask<Result<Dictionary<RecordKey, StoredRecord>>> Stored(ContextRepo repo, CancellationToken cancellationToken)
    {
        var rows = new Dictionary<RecordKey, StoredRecord>();
        RecordKey? after = null;
        while (true)
        {
            var page = await store.List(new RecordFilter(Source, repo.Name, RecordKind.Doc), after, ListPage, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            foreach (var row in page.Value)
            {
                rows[row.Key] = row;
            }

            if (page.Value.Count < ListPage)
            {
                return rows;
            }

            after = page.Value[^1].Key;
        }
    }

    /// <summary>The document as a raw item, or null when git can't read its blob this run (its stored record stays).</summary>
    private async ValueTask<RecordItem?> Item(ContextRepo repo, string path, string blob, CancellationToken cancellationToken)
    {
        var text = await git.Blob(repo.Path, blob, cancellationToken).ConfigureAwait(false);
        return text.IsError
            ? null
            : new RecordItem(RepoDocs.Key(repo.Name, path), RecordKind.Doc, DocChunker.Title(text.Value, path), text.Value, Author: null, Url: null, State: null,
                StateRaw: null, CreatedAt: null, UpdatedAt: null, ClosedAt: null, SourceVersion.From(blob), Links: [], repo.Name, ProfileAudiencePolicy.OwnRepo(repo.Name, githubRepos));
    }

    /// <summary>True when the path fits a chunk id and a record key (no control or format characters, not too long), and no deny glob names it.</summary>
    private static bool Citable(ContextRepo repo, string path) =>
        !repo.Denies(path) && RepoDocs.TryChunkId(repo.Name, path, DocChunker.MaxLine, DocChunker.MaxLine, out _) && RepoDocs.TryKey(repo.Name, path, out _);
}
