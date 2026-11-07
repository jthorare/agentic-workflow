using System.Text;
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

/// <summary>The repo docs feed hashes a committed packet.json as its bytes, a leading byte order mark included (#2193): git's stdout is not decoded.</summary>
[Trait("Lane", "integration")]
public sealed class RepoDocsFeedPacketBytesTests : IDisposable
{
    private const string DocPath = "docs/edited/doc.md";
    private const string PacketPath = "docs/edited/packet.json";
    private const string OldHash = "47f7ec96fcd461a4f3da4b1dca0f8942635a3555ba1ac586e3fcdef2b0034112";
    private static readonly string[] _globs = ["docs/**/*.md"];

    private readonly Scrubbers _scrubbers = new FakeScrubberFactory().For([]).Value;
    private TempGitRepo? _git;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _git?.Dispose();

    [Fact]
    public async Task A_packet_json_with_a_UTF_8_byte_order_mark_hashes_as_committed_so_SND_LIB_017_does_not_fire()
    {
        byte[] packet = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("{\"slug\":\"edited\",\"title\":\"Bom\"}\n")];

        Assert.DoesNotContain(DesignDocCodes.StaleGenerated, await CodesFor(packet));
    }

    [Fact]
    public async Task A_packet_json_with_a_UTF_16_LE_byte_order_mark_hashes_as_committed_so_SND_LIB_017_does_not_fire()
    {
        byte[] packet = [0xFF, 0xFE, .. Encoding.Unicode.GetBytes("{\"slug\":\"edited\",\"title\":\"Wide\"}\n")];

        Assert.DoesNotContain(DesignDocCodes.StaleGenerated, await CodesFor(packet));
    }

    /// <summary>Commits a generated doc whose source-sha256 is the hash of <paramref name="packet"/>'s exact bytes, beside that packet, and pulls the feed once.</summary>
    private async Task<IReadOnlySet<string>> CodesFor(byte[] packet)
    {
        var fixture = Path.Combine(LibraryFeedRig.RepoRoot(), "tests", "fixtures", "library", "broken", "017-edited-generated");
        var doc = (await File.ReadAllTextAsync(Path.Combine(fixture, "doc.md"), Ct)).Replace(OldHash, GeneratedSource.Sha256(packet), StringComparison.Ordinal);
        _git = new TempGitRepo(new Dictionary<string, string>(StringComparer.Ordinal) { [DocPath] = doc, [PacketPath] = "placeholder" });
        await File.WriteAllBytesAsync(Path.Combine(_git.Path, PacketPath), packet, Ct);
        _ = _git.Run("commit", "-q", "-am", "packet with a byte order mark");

        var store = new FakeRecordStore();
        var feed = new RepoDocsFeed([_git.Repo], _globs, [], store, new GitCli(), _scrubbers);
        var page = (await feed.Pull(new RecordPull(After: null, 100, NotBefore: null), Ct)).Value;
        var batch = RecordBatches.From(feed.Source, page, SyncPhase.Incremental, _scrubbers, new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero));
        Assert.Contains(RepoDocs.Key(_git.Repo.Name, DocPath), page.Items.Select(static i => i.Key));
        return batch.Records.Select(static w => (w.Side as DesignDocSideRows)?.Doc).OfType<ParsedDesignDoc>().SelectMany(static d => d.Errors).Select(static e => e.Code).ToHashSet(StringComparer.Ordinal);
    }
}
