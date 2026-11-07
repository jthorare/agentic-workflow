using System.Security.Cryptography;
using System.Text;
using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Docs;

/// <summary>
/// One pass over one library root (review page spec 2026-11-04 §5.2). A root whose listing (path, size, time of every markdown file, with the parser version and scrub ruleset) is the one the cursor holds is
/// not read. Otherwise each file the store holds unchanged (same time, same ruleset) is kept without a read; every other file's first 4 KiB decide whether it is a design doc, and an admitted one is read whole.
/// </summary>
internal sealed class LibraryRootReader(IReadOnlyList<string> githubRepos, IRecordStore store, Scrubbers scrubbers)
{
    private const int ListPage = 1000;

    private readonly string _stamp = DesignDocParser.Stamp(scrubbers.Ruleset.Value);

    internal async ValueTask<Result<LibraryRootPass>> Run(LibraryRoot root, string? known, int room, CancellationToken cancellationToken)
    {
        var listing = LibraryWalker.List(root);
        if (listing.Problem is { } problem)
        {
            return LibraryRootPass.Failed(root.Name, root.Path, problem);
        }

        var fingerprint = Fingerprint(listing.Files);
        if (string.Equals(fingerprint, known, StringComparison.Ordinal))
        {
            return LibraryRootPass.Same(root.Name, root.Path, fingerprint);
        }

        var stored = await Stored(root.Name, cancellationToken).ConfigureAwait(false);
        return stored.IsError ? stored.Error : Diff(root, listing.Files, stored.Value, fingerprint, room);
    }

    private LibraryRootPass Diff(LibraryRoot root, IReadOnlyList<LibraryFile> files, Dictionary<string, StoredRecord> stored, string fingerprint, int room)
    {
        var tally = new Tally(room);
        foreach (var file in files)
        {
            Consider(root.Name, file, stored, tally);
        }

        var removed = stored.Keys.Where(k => !tally.Live.Contains(k)).Order(StringComparer.Ordinal).Select(RecordKey.From).ToList();
        return new LibraryRootPass(root.Name, root.Path, Problem: null, tally.Cut ? null : fingerprint, Unchanged: false, tally.Live.Count, tally.NearMisses, tally.Items, removed);
    }

    private void Consider(string root, LibraryFile file, Dictionary<string, StoredRecord> stored, Tally tally)
    {
        var key = Key(root, file.Relative);
        if (key is null)
        {
            return;
        }

        var held = stored.TryGetValue(key, out var row);
        if (held && Unchanged(row, file))
        {
            _ = tally.Live.Add(key);
            return;
        }

        var head = Head(file);
        if (head is null)
        {
            if (held)
            {
                _ = tally.Live.Add(key);   // the file vanished mid-pass or can't be read now: its stored doc stays until a pass lists the root without it
            }

            return;
        }

        var kind = LibraryAdmission.Classify(file.Relative, head);
        if (kind == LibraryAdmissionKind.NearMiss)
        {
            tally.NearMisses.Add(file.Relative);
        }
        else if (kind == LibraryAdmissionKind.Read)
        {
            _ = tally.Live.Add(key);
            Admit(file, key, tally);
        }
    }

    private void Admit(LibraryFile file, string key, Tally tally)
    {
        if (tally.Items.Count >= tally.Room)
        {
            tally.Cut = true;   // over the limit: the next pull carries on with what the store doesn't hold yet
        }
        else if (Item(file, key) is { } item)
        {
            tally.Items.Add(item);
        }
    }

    private bool Unchanged(StoredRecord? row, LibraryFile file) =>
        row?.UpdatedAt is { } at && at.ToUnixTimeMilliseconds() == file.Modified.ToUnixTimeMilliseconds() && string.Equals(row.Ruleset, _stamp, StringComparison.Ordinal);

    private RecordItem? Item(LibraryFile file, string key)
    {
        var bytes = ReadUpTo(file.FullPath, DesignDocParser.MaxBytes + 1);
        if (bytes is null)
        {
            return null;
        }

        var text = Encoding.UTF8.GetString(bytes);
        var repos = DesignDocParser.Parse(key, text).FrontMatter?.Repos ?? [];
        return new RecordItem(
            RecordKey.From(key), RecordKind.Doc, DocChunker.Title(text, file.Relative), text, Author: null, Url: null, State: null, StateRaw: null,
            CreatedAt: null, file.Modified, ClosedAt: null, SourceVersion.From(Convert.ToHexStringLower(SHA256.HashData(bytes))), Links: [], Repo: null, ProfileAudiencePolicy.LibraryDoc(repos, githubRepos));
    }

    private static string? Head(LibraryFile file)
    {
        var bytes = ReadUpTo(file.FullPath, LibraryAdmission.HeadBytes);
        return bytes is null ? null : Encoding.UTF8.GetString(bytes);
    }

    private static byte[]? ReadUpTo(string path, int count)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var buffer = new byte[(int)Math.Min(count, stream.Length)];
            var read = 0;
            while (read < buffer.Length)
            {
                var n = stream.Read(buffer, read, buffer.Length - read);
                if (n == 0)
                {
                    break;
                }

                read += n;
            }

            return read == buffer.Length ? buffer : buffer[..read];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)   // boundary: a file that vanished or can't be read this run
        {
            return null;
        }
    }

    private static string? Key(string root, string relative)
    {
        var text = $"doc:{root}/{relative}";
        return RecordKey.TryFrom(text, out _) ? text : null;
    }

    private string Fingerprint(IReadOnlyList<LibraryFile> files)
    {
        var text = new StringBuilder(_stamp);
        foreach (var file in files)
        {
            _ = text.Append('\n').Append(file.Relative).Append('|').Append(file.Size).Append('|').Append(file.Modified.ToUnixTimeMilliseconds());
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..16];
    }

    // The live library doc records of one root, by key text (tombstones are no longer held).
    private async ValueTask<Result<Dictionary<string, StoredRecord>>> Stored(string root, CancellationToken cancellationToken)
    {
        var rows = new Dictionary<string, StoredRecord>(StringComparer.Ordinal);
        var prefix = $"doc:{root}/";
        RecordKey? after = null;
        while (true)
        {
            var page = await store.List(new RecordFilter(LibrarySource.Id, Repo: null, RecordKind.Doc), after, ListPage, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            foreach (var row in page.Value.Where(r => r.Key.Value.StartsWith(prefix, StringComparison.Ordinal)))
            {
                rows[row.Key.Value] = row;
            }

            if (page.Value.Count < ListPage)
            {
                return rows;
            }

            after = page.Value[^1].Key;
        }
    }

    private sealed class Tally(int room)
    {
        internal int Room { get; } = room;

        internal HashSet<string> Live { get; } = new(StringComparer.Ordinal);

        internal List<RecordItem> Items { get; } = [];

        internal List<string> NearMisses { get; } = [];

        internal bool Cut { get; set; }
    }
}
