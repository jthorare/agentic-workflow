using System.Text;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Infrastructure;

namespace Aw.Context.Tests.Sync;

/// <summary>
/// The repo docs feed decides SND-LIB-017 for a generated doc against the packet.json committed beside it (#2193), as the library read does (#2194):
/// a doc is read again when only its packet changed, and a doc with no packet beside it is not checked.
/// </summary>
[Trait("Lane", "integration")]
public sealed class RepoDocsFeedGeneratedSourceTests : IDisposable
{
    private const string DocPath = "docs/edited/doc.md";
    private const string PacketPath = "docs/edited/packet.json";
    private const string OldHash = "47f7ec96fcd461a4f3da4b1dca0f8942635a3555ba1ac586e3fcdef2b0034112";
    private static readonly string[] _globs = ["docs/**/*.md"];

    private readonly FakeRecordStore _store = new();
    private readonly Scrubbers _scrubbers = new FakeScrubberFactory().For([]).Value;
    private TempGitRepo? _git;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _git?.Dispose();

    [Fact]
    public async Task A_generated_doc_whose_committed_packet_hashes_to_something_else_shows_SND_LIB_017()
    {
        var fixture = Path.Combine(LibraryFeedRig.RepoRoot(), "tests", "fixtures", "library", "broken", "017-edited-generated");
        Commit(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [DocPath] = await File.ReadAllTextAsync(Path.Combine(fixture, "doc.md"), Ct),
            [PacketPath] = await File.ReadAllTextAsync(Path.Combine(fixture, "packet.json"), Ct),
        });

        var (items, errors) = await Pull();

        Assert.Contains(RepoDocs.Key(_git!.Repo.Name, DocPath), items);
        Assert.Contains(DesignDocCodes.StaleGenerated, errors);
    }

    [Fact]
    public async Task A_commit_that_changes_only_the_packet_so_it_matches_reads_the_doc_again_and_clears_SND_LIB_017()
    {
        const string stale = "{\"slug\":\"edited\",\"title\":\"Edited after generating\"}\n";
        const string regenerated = "{\"slug\":\"edited\",\"title\":\"Regenerated\"}\n";
        var fixture = Path.Combine(LibraryFeedRig.RepoRoot(), "tests", "fixtures", "library", "broken", "017-edited-generated");
        var doc = (await File.ReadAllTextAsync(Path.Combine(fixture, "doc.md"), Ct))
            .Replace(OldHash, GeneratedSource.Sha256(Encoding.UTF8.GetBytes(regenerated)), StringComparison.Ordinal);
        Commit(new Dictionary<string, string>(StringComparer.Ordinal) { [DocPath] = doc, [PacketPath] = stale });
        _ = await Pull();
        var key = RepoDocs.Key(_git!.Repo.Name, DocPath);
        var before = (await _store.Get(key, Ct)).Value!.Version;

        await File.WriteAllTextAsync(Path.Combine(_git.Path, PacketPath), regenerated, Ct);
        _ = _git.Run("commit", "-q", "-am", "regenerate the packet only");
        var (items, errors) = await Pull();

        Assert.Contains(key, items);
        Assert.DoesNotContain(DesignDocCodes.StaleGenerated, errors);
        Assert.NotEqual(before, (await _store.Get(key, Ct)).Value!.Version);
    }

    [Fact]
    public async Task A_generated_doc_with_no_packet_beside_it_is_not_checked()
    {
        var fixture = Path.Combine(LibraryFeedRig.RepoRoot(), "tests", "fixtures", "library", "broken", "017-edited-generated");
        Commit(new Dictionary<string, string>(StringComparer.Ordinal) { [DocPath] = await File.ReadAllTextAsync(Path.Combine(fixture, "doc.md"), Ct) });

        var (items, errors) = await Pull();

        Assert.Contains(RepoDocs.Key(_git!.Repo.Name, DocPath), items);
        Assert.DoesNotContain(DesignDocCodes.StaleGenerated, errors);
    }

    private void Commit(Dictionary<string, string> files) => _git = new TempGitRepo(files);

    /// <summary>One sync pull of the feed as the driver does it: the keys it handed over and the codes the doc's parse carries.</summary>
    private async Task<(IReadOnlyList<RecordKey> Items, IReadOnlySet<string> Errors)> Pull()
    {
        var feed = new RepoDocsFeed([_git!.Repo], _globs, [], _store, new GitCli(), _scrubbers);
        var position = (await _store.Position(feed.Source, Ct)).Value;
        var page = (await feed.Pull(new RecordPull(position.Cursor, 100, NotBefore: null), Ct)).Value;
        var batch = RecordBatches.From(feed.Source, page, SyncPhase.Incremental, _scrubbers, new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero));
        if (page.Items.Count > 0 || page.Removed.Count > 0)
        {
            _ = await _store.Commit(batch, Ct);
        }

        var errors = batch.Records.Select(static w => (w.Side as DesignDocSideRows)?.Doc).OfType<ParsedDesignDoc>().SelectMany(static d => d.Errors).Select(static e => e.Code).ToHashSet(StringComparer.Ordinal);
        return (page.Items.Select(static i => i.Key).ToList(), errors);
    }
}
