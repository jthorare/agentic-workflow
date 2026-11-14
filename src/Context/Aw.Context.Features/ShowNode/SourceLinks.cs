using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Trust;

namespace Aw.Context.Features.ShowNode;

/// <summary>
/// The links of a node's source (#1702): a record's stored permalink (for a record the caller may read), or the file on GitHub from the checkout's
/// origin, pinned to the map's commit when the file is in the repo the map was built for. Anything that can't be read or isn't allowed is a null link
/// and the node still shows; a link is never made up.
/// </summary>
internal sealed class SourceLinks(ContextScope scope, VisibleMap map, IEgressScrubber egress, IReadOnlySet<string> linearTeams, IRecordStore records, IRepoRemote remotes)
{
    private readonly HashSet<RecordKey> _mapRecords = scope.Caller.Kind == ContextCallerKind.Session
        ? [.. map.Graph.Nodes.Where(n => n.Key.Kind == NodeKind.Record && RecordKey.TryFrom(n.Key.Name, out _)).Select(n => RecordKey.From(n.Key.Name))]
        : [];

    private AudienceScope? _audience;

    public async ValueTask<Uri?> Of(SourceTarget? target, CancellationToken cancellationToken)
    {
        if (target is null)
        {
            return null;
        }

        return target.Record is { } record ? await ForRecord(record, cancellationToken).ConfigureAwait(false) : await ForFile(target, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The file on the caller's disk, for a human only: a session must not learn where a checkout lives. It is the repo's checkout path joined with the
    /// file's path, and null when the file is not in a repo of the scope or the path would leave the checkout.
    /// </summary>
    /// <param name="scope">The caller's scope.</param>
    /// <param name="target">The node's source target.</param>
    /// <returns>The absolute path, or null.</returns>
    public static string? LocalPath(ContextScope scope, SourceTarget? target)
    {
        if (scope.Caller.Kind == ContextCallerKind.Session || target?.Record is not null || target?.Path is not { Length: > 0 } path)
        {
            return null;
        }

        var repo = scope.Repos.FirstOrDefault(r => string.Equals(r.Name.Value, target.Repo, StringComparison.Ordinal));
        return repo is null ? null : Inside(repo.Path, path);
    }

    private static string? Inside(string checkout, string path)
    {
        if (!Path.IsPathRooted(checkout) || Path.IsPathRooted(path))
        {
            return null;
        }

        var root = Path.GetFullPath(checkout).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, path));
        return full.StartsWith(root, StringComparison.Ordinal) && full.Length > root.Length ? full : null;
    }

    private async ValueTask<Uri?> ForRecord(RecordKey key, CancellationToken cancellationToken)
    {
        var stored = await records.Get(key, cancellationToken).ConfigureAwait(false);
        if (stored.IsError || stored.Value is not { RemovedAt: null, Blocked: false } record || !await Allows(record, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return RecordPermalinks.Of(record, linearTeams, egress).Permalink;
    }

    // A human is not scoped by audience (spec §4.4) and already sees the record's node; a session reads what its repos' audiences and its scoped map give.
    private async ValueTask<bool> Allows(StoredRecord record, CancellationToken cancellationToken)
    {
        if (scope.Caller.Kind != ContextCallerKind.Session)
        {
            return true;
        }

        _audience ??= await SessionAudience(cancellationToken).ConfigureAwait(false);
        return _audience?.Allows(record.Audience, record.Key) ?? false;
    }

    private async ValueTask<AudienceScope?> SessionAudience(CancellationToken cancellationToken)
    {
        var settings = SettingsReader.Read(scope.Settings);
        if (settings.IsError)
        {
            return null;
        }

        var allowed = await RecordMask.Allowed(settings.Value, scope.Repos, ContextCallerKind.Session, cancellationToken).ConfigureAwait(false);
        return allowed.IsError ? null : new AudienceScope(allowed.Value) { Records = _mapRecords };
    }

    private async ValueTask<Uri?> ForFile(SourceTarget target, CancellationToken cancellationToken)
    {
        var repo = scope.Repos.FirstOrDefault(r => string.Equals(r.Name.Value, target.Repo, StringComparison.Ordinal));
        if (repo is null || target.Path is not { } path)
        {
            return null;
        }

        var origin = await remotes.Origin(repo, cancellationToken).ConfigureAwait(false);
        if (origin.IsError || GithubRemote.Parse(origin.Value) is not { } github)
        {
            return null;
        }

        var version = map.Snapshot.Version;
        return github.Blob(string.Equals(version.Repo.Value, repo.Name.Value, StringComparison.Ordinal) ? version.Commit : null, path, egress);
    }
}
