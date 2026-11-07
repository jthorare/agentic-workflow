using Aw.Context.Core;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Docs;

/// <summary>
/// The library record feed (review page spec 2026-11-04 §5.2): per root the design docs under it, each a 'doc' record keyed 'doc:&lt;root&gt;/&lt;path&gt;' whose version is the sha256 of the file's bytes. The text is raw: the sync
/// driver cleans and chunks it (<see cref="RecordBatches"/>), and itemizes it as a design doc. The cursor holds one fingerprint per root, so a pull over unchanged roots is an empty page and a changed root is diffed against the store
/// (<see cref="LibraryRootReader"/>). A root that can't be listed fails nothing but itself: its stored docs stay and its report says why; the pull fails only when no root could be listed. What each pass found is left
/// in <see cref="ILibraryStatus"/> for the doctor row and <see cref="Core.Facade.IContextLibrary.Roots"/>.
/// </summary>
internal sealed class LibraryFeed(LibrarySetup setup, IRecordStore store, Scrubbers scrubbers, ILibraryStatus status, TimeProvider clock) : RecordFeed(LibrarySource.Id)
{
    private const string Format = "lib1";

    private readonly LibraryRootReader _reader = new(setup.GithubRepos, store, scrubbers);

    public override async ValueTask<Result<RecordPage>> Pull(RecordPull pull, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pull);
        var known = Parts(pull.After);
        var before = status.Read().ToDictionary(static r => r.Root, StringComparer.Ordinal);
        var items = new List<RecordItem>();
        var removed = new List<RecordKey>();
        var cursor = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var reports = new List<LibraryRootReport>();
        foreach (var root in setup.Roots)
        {
            var last = before.GetValueOrDefault(root.Name);
            var trusted = last is { Problem: null } ? known.GetValueOrDefault(root.Name) : null;   // the "nothing changed" shortcut needs a good last report to copy: after a failed pass or a lost status file the root is diffed again
            var pass = await _reader.Run(root, trusted, Math.Max(pull.Limit, 1) - items.Count, cancellationToken).ConfigureAwait(false);
            if (pass.IsError)
            {
                return pass.Error;
            }

            items.AddRange(pass.Value.Items);
            removed.AddRange(pass.Value.Removed);
            var fingerprint = pass.Value.Problem is null ? pass.Value.Fingerprint : known.GetValueOrDefault(root.Name);   // an unreadable root keeps its place in the cursor
            if (fingerprint is not null)
            {
                cursor[root.Name] = fingerprint;
            }

            reports.Add(Report(pass.Value, last));
        }

        return await Finish(reports, new RecordPage(items, removed, SourceCursor.From(Cursor(cursor)), CursorReset: false), cancellationToken).ConfigureAwait(false);
    }

    // The pass's reports are left for the doctor row; a pull with no readable root fails, and docs of a root the profile no longer names are removed.
    private async ValueTask<Result<RecordPage>> Finish(List<LibraryRootReport> reports, RecordPage page, CancellationToken cancellationToken)
    {
        status.Write(reports);
        if (reports.Count > 0 && reports.TrueForAll(static r => r.Problem is not null))
        {
            return ContextErrors.SourceUnavailable.With($"the library has no readable root ({string.Join(", ", reports.Select(static r => r.Root))}); sindri doctor's library row says why");   // roots by name: a session can read a job's error, and paths are the owner's
        }

        var strays = await _reader.Strays([.. setup.Roots.Select(static r => r.Name)], cancellationToken).ConfigureAwait(false);
        return strays.IsError ? strays.Error : page with { Removed = [.. page.Removed, .. strays.Value] };
    }

    // A root that was not read (unchanged) keeps the docs and near misses its last report counted.
    private LibraryRootReport Report(LibraryRootPass pass, LibraryRootReport? last) =>
        pass.Unchanged && last is not null
            ? last with { Path = pass.Path, Problem = null, LastRead = clock.GetUtcNow() }
            : new LibraryRootReport(pass.Root, pass.Path, pass.Problem, pass.Docs, pass.NearMisses, clock.GetUtcNow());

    private static string Cursor(SortedDictionary<string, string> parts) => string.Concat(Format, string.Concat(parts.Select(static p => $";{p.Key}={p.Value}")));

    private static Dictionary<string, string> Parts(SourceCursor? after)
    {
        var parts = new Dictionary<string, string>(StringComparer.Ordinal);
        if (after is not { } cursor)
        {
            return parts;
        }

        var tokens = cursor.Value.Split(';');
        if (!string.Equals(tokens[0], Format, StringComparison.Ordinal))
        {
            return parts;   // a cursor this feed didn't write is no cursor: every root is read in full
        }

        foreach (var part in tokens.Skip(1))
        {
            if (part.Split('=') is [var name, var fingerprint])
            {
                parts[name] = fingerprint;
            }
        }

        return parts;
    }
}
