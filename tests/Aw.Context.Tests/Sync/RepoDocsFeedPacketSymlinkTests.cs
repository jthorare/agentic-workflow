using Aw.Context.Core.Docs;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Infrastructure;

namespace Aw.Context.Tests.Sync;

/// <summary>A packet.json that is a symlink in the repo is not a source for SND-LIB-017 (#2193), as the library walker refuses it.</summary>
[Trait("Lane", "integration")]
public sealed class RepoDocsFeedPacketSymlinkTests : IDisposable
{
    private const string DocPath = "docs/edited/doc.md";
    private static readonly string[] _globs = ["docs/**/*.md"];

    private readonly Scrubbers _scrubbers = new FakeScrubberFactory().For([]).Value;
    private TempGitRepo? _git;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _git?.Dispose();

    [Fact]
    public async Task A_symlinked_packet_json_is_no_source_so_a_stale_generated_doc_shows_no_SND_LIB_017()
    {
        var fixture = Path.Combine(LibraryFeedRig.RepoRoot(), "tests", "fixtures", "library", "broken", "017-edited-generated");
        _git = new TempGitRepo(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [DocPath] = await File.ReadAllTextAsync(Path.Combine(fixture, "doc.md"), Ct),
            ["real.json"] = await File.ReadAllTextAsync(Path.Combine(fixture, "packet.json"), Ct),
        });
        _ = File.CreateSymbolicLink(Path.Combine(_git.Path, "docs", "edited", "packet.json"), Path.Combine(_git.Path, "real.json"));
        _ = _git.Run("add", "-A");
        _ = _git.Run("commit", "-q", "-m", "link the packet");

        var (_, symlinks) = (await new GitCli().TreeWithLinks(_git.Path, _git.Run("rev-parse", "HEAD"), Ct)).Value;
        Assert.Contains("docs/edited/packet.json", symlinks);
        Assert.DoesNotContain("real.json", symlinks);

        var store = new FakeRecordStore();
        var feed = new RepoDocsFeed([_git.Repo], _globs, [], store, new GitCli(), _scrubbers);
        var page = (await feed.Pull(new RecordPull(After: null, 100, NotBefore: null), Ct)).Value;
        var batch = RecordBatches.From(feed.Source, page, SyncPhase.Incremental, _scrubbers, new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero));
        var codes = batch.Records.Select(static w => (w.Side as DesignDocSideRows)?.Doc).OfType<ParsedDesignDoc>().SelectMany(static d => d.Errors).Select(static e => e.Code).ToHashSet(StringComparer.Ordinal);

        Assert.Contains(RepoDocs.Key(_git.Repo.Name, DocPath), page.Items.Select(static i => i.Key));
        Assert.DoesNotContain(DesignDocCodes.StaleGenerated, codes);
        Assert.Equal((await new GitCli().Tree(_git.Path, _git.Run("rev-parse", "HEAD"), Ct)).Value[DocPath], page.Items[0].Version.Value);   // the doc's version is its blob id: no packet counted, nothing folded
    }
}
