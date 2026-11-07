using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Library;
using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.ReviewListFacts;

/// <summary>
/// The library page's facts over Context (PR review page spec §6.1): every doc the caller may read with its errors, each file written with the home folder as <c language="text">~</c>, and the folders that couldn't be read.
/// A finding's text is its message and its fix, with the record key in the message replaced by the file.
/// </summary>
/// <param name="approvals">The approved profile.</param>
/// <param name="callers">The current caller.</param>
/// <param name="engine">Context's facade: settings and the library reads.</param>
/// <param name="host">The machine's home folders.</param>
internal sealed class ContextLibraryPageFacts(IApprovedProfile approvals, ICallerContextAccessor callers, IContextEngine engine, LibraryHost host) : ILibraryPageFacts
{
    public async ValueTask<Result<LibraryPageFacts>> Read(CancellationToken cancellationToken)
    {
        var read = await ReviewScopes.Read(approvals, callers, engine, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var (settings, scope, audience) = read.Value;
        var docs = await engine.ListDocs(new LibraryDocFilter(), audience, cancellationToken).ConfigureAwait(false);
        if (docs.IsError)
        {
            return docs.Error;
        }

        var folders = LibraryFolders.Of(settings.Library, host);
        var broken = docs.Value.Where(static d => d.Errors.Count > 0).Select(d => Broken(d, folders)).ToList();

        // why: a session's Roots answers SND-CONTEXT-020 and a failed read says nothing about the folders, so both show none
        var roots = await engine.Roots(scope, cancellationToken).ConfigureAwait(false);
        var unreadable = roots.IsError ? [] : roots.Value.Where(static r => r.State == LibraryRootState.Unreadable).Select(r => new ReviewUnreadableRoot(folders.Root(r.Root), r.Reason ?? "it can't be read")).ToList();

        // why: Context lists a near miss only as a count on its root today; it adds the finding (rule, line, message, fix) to LibraryRootStatus in its own change, and this list starts then
        var listed = roots.IsError || read.Value.Scope.Caller.Kind == ContextCallerKind.Session ? [] : await RootFacts(roots.Value, new RootCounts(docs.Value), read.Value, folders, cancellationToken).ConfigureAwait(false);
        return new LibraryPageFacts(docs.Value.Count, broken, NearMisses: [], unreadable, listed);
    }

    // why: the aw root and the vaults come from the feed's root states (what its last pass found); a repo root has no state of its own, so its docs and errors are counted from the docs the caller may read and its scan time is the repo-docs source's last sync
    private async ValueTask<List<LibraryRootFacts>> RootFacts(IReadOnlyList<LibraryRootStatus> states, RootCounts counts, Scoped read, LibraryFolders folders, CancellationToken cancellationToken)
    {
        var roots = states.Select(s => Stored(s, counts, read.Settings, folders)).ToList();
        var progress = await engine.Progress(read.Scope, cancellationToken).ConfigureAwait(false);
        var scanned = progress.IsError ? null : progress.Value.FirstOrDefault(static p => string.Equals(p.Source.Value, RepoDocs.Id.Value, StringComparison.Ordinal))?.LastSync;
        roots.AddRange(read.Scope.Repos.Select(r => Repo(r, counts, read.Settings, folders, scanned)));
        return roots;
    }

    private LibraryRootFacts Stored(LibraryRootStatus state, RootCounts counts, ContextSettings settings, LibraryFolders folders)
    {
        var isAw = string.Equals(state.Root, "aw", StringComparison.Ordinal);
        var path = isAw && settings.Library is { AwRootOn: false } ? folders.Show(host.StateDefault) : folders.Root(state.Root);
        var deny = settings.Library?.Vaults.FirstOrDefault(v => string.Equals(v.Name, state.Root, StringComparison.Ordinal))?.Deny ?? [];
        var unreadable = state.State == LibraryRootState.Unreadable ? state.Reason ?? "it can't be read" : null;
        return new LibraryRootFacts(state.Root, isAw ? LibraryRootKind.Aw : LibraryRootKind.Vault, path, Repo: null, deny, state.State == LibraryRootState.Off, state.LastRead, state.Docs, counts.Errors(state.Root), unreadable);
    }

    // why: a repo root is named by its GitHub repo when context.sources.github.repos has one whose name is the profile repo's, else by the profile repo's own name
    private static LibraryRootFacts Repo(ContextRepo repo, RootCounts counts, ContextSettings settings, LibraryFolders folders, DateTimeOffset? scanned)
    {
        var key = "repo:" + repo.Name.Value;
        var slug = (settings.GitHub?.Repos ?? []).FirstOrDefault(r => string.Equals(r[(r.IndexOf('/', StringComparison.Ordinal) + 1)..], repo.Name.Value, StringComparison.OrdinalIgnoreCase)) ?? repo.Name.Value;
        return new LibraryRootFacts("repo:" + slug, LibraryRootKind.Repo, folders.Show(repo.Path), slug, Deny: [], Off: false, scanned, counts.Docs(key), counts.Errors(key), Unreadable: null);
    }

    private sealed class RootCounts(IReadOnlyList<LibraryDoc> docs)
    {
        private readonly Dictionary<string, int> _docs = docs.GroupBy(static d => d.Root, StringComparer.Ordinal).ToDictionary(static g => g.Key, static g => g.Count(), StringComparer.Ordinal);
        private readonly Dictionary<string, int> _errors = docs.Where(static d => d.Errors.Count > 0).GroupBy(static d => d.Root, StringComparer.Ordinal).ToDictionary(static g => g.Key, static g => g.Count(), StringComparer.Ordinal);

        internal int Docs(string root) => _docs.GetValueOrDefault(root);

        internal int Errors(string root) => _errors.GetValueOrDefault(root);
    }

    private static LibraryBrokenDoc Broken(LibraryDoc doc, LibraryFolders folders)
    {
        var path = folders.File(doc.Key.Value);
        return new LibraryBrokenDoc(doc.Slug ?? Path.GetFileNameWithoutExtension(path), path, [.. doc.Errors.Select(e => new LibraryDocError(e.Code, LibraryErrorText.Of(e, doc.Key.Value, path)))]);
    }
}
