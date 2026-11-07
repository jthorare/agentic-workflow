using System.Globalization;
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

    // A generated doc is stored as <doc sha>+<packet.json sha> (the second part empty when none is beside it); a plain doc as its sha.
    private const string GeneratedMark = "+";

    private readonly string _stamp = DesignDocParser.Stamp(scrubbers.Ruleset.Value);

    // A doc's audience comes from the synced repos, so they are part of what a pass compared with: the cursor part is the listing's hash (16 characters), then this hash of the sorted repos (8).
    private readonly string _repos = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', githubRepos.Select(static r => r.ToLowerInvariant()).Order(StringComparer.Ordinal)))))[..8];

    internal async ValueTask<Result<LibraryRootPass>> Run(LibraryRoot root, string? known, int room, CancellationToken cancellationToken)
    {
        var listing = LibraryWalker.List(root);
        if (listing.Problem is { } problem)
        {
            return LibraryRootPass.Failed(root.Name, root.Path, problem);
        }

        if (listing.Files.Count == 0 && await EmptyWhileStored(root, cancellationToken).ConfigureAwait(false) is { } kept)
        {
            return kept.IsError ? kept.Error : LibraryRootPass.Failed(root.Name, root.Path, kept.Value);
        }

        var fingerprint = Fingerprint(listing.Files) + _repos;
        if (string.Equals(fingerprint, known, StringComparison.Ordinal))
        {
            return LibraryRootPass.Same(root.Name, root.Path, fingerprint);
        }

        var stored = await Stored(root.Name, cancellationToken).ConfigureAwait(false);
        var reaudience = known is null || !known.EndsWith(_repos, StringComparison.Ordinal);   // the synced repos changed: every doc's audience is worked out again
        return stored.IsError ? stored.Error : Diff(root, listing.Files, stored.Value, new Tally(room, reaudience), fingerprint);
    }

    // An unmounted drive leaves an empty mount point: a root that lists nothing while docs are stored for it is a problem, not a deletion.
    private async ValueTask<Result<string>?> EmptyWhileStored(LibraryRoot root, CancellationToken cancellationToken)
    {
        var stored = await Stored(root.Name, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        return stored.Value.Count == 0 ? null : Results.Success(string.Create(CultureInfo.InvariantCulture, $"{root.Path} is empty, {stored.Value.Count} docs kept"));
    }

    /// <summary>The stored library docs whose root is not one of <paramref name="roots"/>: a vault the profile dropped, or the 'aw' root turned off.</summary>
    /// <param name="roots">The roots the profile names now.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Their keys.</returns>
    internal async ValueTask<Result<List<RecordKey>>> Strays(IReadOnlyCollection<string> roots, CancellationToken cancellationToken)
    {
        var strays = new List<RecordKey>();
        RecordKey? after = null;
        while (true)
        {
            var page = await store.List(new RecordFilter(LibrarySource.Id, Repo: null, RecordKind.Doc), after, ListPage, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            strays.AddRange(page.Value.Where(r => !roots.Contains(RootOf(r.Key.Value), StringComparer.Ordinal)).Select(static r => r.Key));
            if (page.Value.Count < ListPage)
            {
                return strays;
            }

            after = page.Value[^1].Key;
        }
    }

    // 'doc:<root>/<path>': the root is the segment after 'doc:' up to the first '/'
    private static string RootOf(string key) => key.Length > 4 && key.IndexOf('/', 4) is var end and > 4 ? key[4..end] : string.Empty;

    private LibraryRootPass Diff(LibraryRoot root, IReadOnlyList<LibraryFile> files, Dictionary<string, StoredRecord> stored, Tally tally, string fingerprint)
    {
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
        if (held && !tally.Reaudience && Unchanged(row, file))
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
        if (kind == LibraryAdmissionKind.NearMiss && LibraryAdmission.NearMiss(file.Relative, head) is { } miss)
        {
            tally.NearMisses.Add(new DesignDocFinding(file.Relative, miss));
        }
        else if (kind == LibraryAdmissionKind.Read)
        {
            _ = tally.Live.Add(key);
            Admit(file, key, row, tally);
        }
    }

    // A file whose stored doc already holds its bytes, ruleset and audience is not emitted again, whatever its time says (rsync, a sync tool or a checkout touches files without editing them).
    private void Admit(LibraryFile file, string key, StoredRecord? row, Tally tally)
    {
        var bytes = ReadUpTo(file.FullPath, DesignDocParser.MaxBytes + 1);
        if (bytes is null)
        {
            return;   // vanished mid-pass: it is live if it was stored, and the next pass settles it
        }

        var text = Encoding.UTF8.GetString(bytes);
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var frontMatter = DesignDocParser.Parse(key, text).FrontMatter;
        var audience = ProfileAudiencePolicy.LibraryDoc(frontMatter?.Repos ?? [], githubRepos);
        var (version, packetSha) = Versioned(file, sha, frontMatter);
        if (row is not null && Current(row, version, audience))
        {
            return;
        }

        if (tally.Items.Count >= tally.Room)
        {
            tally.Cut = true;   // over the limit: the next pull carries on with what the store doesn't hold yet
        }
        else
        {
            tally.Items.Add(Item(file, key, text, (version, audience), packetSha));
        }
    }

    // A plain doc's version is its sha; a generated doc's also holds the sha of the packet.json beside it (empty when there is none), so a change to either is a new version.
    private static (string Version, string? PacketSha) Versioned(LibraryFile file, string sha, DesignDocFrontMatter? frontMatter)
    {
        if (!GeneratedSource.IsGenerated(frontMatter))
        {
            return (sha, null);
        }

        var packetSha = file.Packet?.Sha256() ?? string.Empty;
        return (sha + GeneratedMark + packetSha, packetSha.Length == 0 ? null : packetSha);
    }

    private bool Current(StoredRecord row, string version, AudienceKey audience) =>
        string.Equals(row.Version.Value, version, StringComparison.Ordinal) && string.Equals(row.Ruleset, _stamp, StringComparison.Ordinal) && Equals(row.Audience, audience);

    // why: a generated doc's version also holds its packet.json, whose edit leaves the doc's time alone, so the doc's time alone never settles it
    private bool Unchanged(StoredRecord? row, LibraryFile file) =>
        row is not null && !row.Version.Value.Contains(GeneratedMark, StringComparison.Ordinal) && row.UpdatedAt is { } at && at.ToUnixTimeMilliseconds() == file.Modified.ToUnixTimeMilliseconds() && string.Equals(row.Ruleset, _stamp, StringComparison.Ordinal);

    private static RecordItem Item(LibraryFile file, string key, string text, (string Version, AudienceKey Audience) read, string? packetSha) =>
        new(RecordKey.From(key), RecordKind.Doc, DocChunker.Title(text, file.Relative), text, Author: null, Url: null, State: null, StateRaw: null,
            CreatedAt: null, file.Modified, ClosedAt: null, SourceVersion.From(read.Version), Links: [], Repo: null, read.Audience, SourceSha256: packetSha);

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
            if (file.Packet is { } packet)
            {
                _ = text.Append("|packet|").Append(packet.Size).Append('|').Append(packet.Modified.ToUnixTimeMilliseconds());
            }
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

    private sealed class Tally(int room, bool reaudience)
    {
        internal int Room { get; } = room;

        internal bool Reaudience { get; } = reaudience;

        internal HashSet<string> Live { get; } = new(StringComparer.Ordinal);

        internal List<RecordItem> Items { get; } = [];

        internal List<DesignDocFinding> NearMisses { get; } = [];

        internal bool Cut { get; set; }
    }
}
