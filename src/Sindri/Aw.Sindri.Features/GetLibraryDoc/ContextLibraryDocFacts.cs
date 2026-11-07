using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Library;
using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.GetLibraryDoc;

/// <summary>
/// One design doc over Context (PR review page spec §6.1). The audience rule is the Reviews list's (<see cref="ReviewScopes"/>): a human reads the docs of every repo of the profile, a session its own repo's.
/// Two scopes can share a doc id; the doc at the lowest path answers, which is the one that keeps the id (spec §4.2).
/// </summary>
/// <param name="approvals">The approved profile.</param>
/// <param name="callers">The current caller.</param>
/// <param name="engine">Context's facade.</param>
/// <param name="host">The machine's home folders, to write a path with <c language="text">~</c>.</param>
internal sealed class ContextLibraryDocFacts(IApprovedProfile approvals, ICallerContextAccessor callers, IContextEngine engine, LibraryHost host) : ILibraryDocFacts
{
    public async ValueTask<Result<LibraryDocView?>> Find(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        var read = await ReviewScopes.Read(approvals, callers, engine, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var (settings, _, audience) = read.Value;
        var docs = await engine.ListDocs(new LibraryDocFilter(), audience, cancellationToken).ConfigureAwait(false);
        if (docs.IsError)
        {
            return docs.Error;
        }

        var doc = docs.Value.Where(d => string.Equals(d.Slug, id, StringComparison.Ordinal)).OrderBy(static d => d.Key.Value, StringComparer.Ordinal).FirstOrDefault();
        return Results.Success(doc is null ? null : View(doc, LibraryFolders.Of(settings.Library, host)));
    }

    private static LibraryDocView View(LibraryDoc doc, LibraryFolders folders)
    {
        var path = folders.File(doc.Key.Value);
        return new LibraryDocView(
            doc.Slug ?? string.Empty, doc.Root, doc.Scope, doc.Kind, doc.Status, doc.Project, doc.Repos, doc.Issues, doc.Title, path,
            [.. doc.Errors.Select(e => new LibraryDocErrorView(e.Code, LibraryErrorText.Of(e, doc.Key.Value, path)))]);
    }
}
