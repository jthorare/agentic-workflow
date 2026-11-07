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
        return new LibraryPageFacts(docs.Value.Count, broken, NearMisses: [], unreadable);
    }

    private static LibraryBrokenDoc Broken(LibraryDoc doc, LibraryFolders folders)
    {
        var path = folders.File(doc.Key.Value);
        return new LibraryBrokenDoc(doc.Slug ?? System.IO.Path.GetFileNameWithoutExtension(path), path, [.. doc.Errors.Select(e => new LibraryDocError(e.Code, Text(e, doc.Key.Value, path)))]);
    }

    internal static string Text(DesignDocError error, string key, string path) =>
        error.Message.Replace(key, path, StringComparison.Ordinal).TrimEnd('.') + ". " + error.Fix.TrimEnd('.');
}
