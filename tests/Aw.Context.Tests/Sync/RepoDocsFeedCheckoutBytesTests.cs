using System.Text;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Infrastructure;
using Aw.Kernel.Testing;

namespace Aw.Context.Tests.Sync;

/// <summary>
/// SND-LIB-017 compares a generated doc's source-sha256 with the packet.json bytes a checkout writes (#2199): write_packet.py hashes the working-tree file,
/// so a packet whose .gitattributes asks for CRLF is hashed as the CRLF form, not as the LF blob git stores.
/// </summary>
[Trait("Lane", "integration")]
public sealed class RepoDocsFeedCheckoutBytesTests : IDisposable
{
    private const string DocPath = "docs/edited/doc.md";
    private const string PacketPath = "docs/edited/packet.json";
    private const string OldHash = "47f7ec96fcd461a4f3da4b1dca0f8942635a3555ba1ac586e3fcdef2b0034112";
    private const string PacketLf = "{\"slug\":\"edited\",\"title\":\"Checked out\"}\n";
    private const string Crlf = "packet.json text eol=crlf\n";
    private static readonly string[] _globs = ["docs/**/*.md"];

    private readonly Scrubbers _scrubbers = new FakeScrubberFactory().For([]).Value;
    private TempGitRepo? _git;
    private string? _scratch;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _git?.Dispose();
        if (_scratch is not null)
        {
            Directory.Delete(_scratch, recursive: true);
        }
    }

    [Fact]
    public async Task A_packet_checked_out_with_crlf_by_gitattributes_matches_its_doc_and_shows_no_SND_LIB_017()
    {
        var crlfPacket = PacketLf.Replace("\n", "\r\n", StringComparison.Ordinal);

        var codes = await CodesFor(Crlf, GeneratedSource.Sha256(Encoding.UTF8.GetBytes(crlfPacket)));

        Assert.DoesNotContain(DesignDocCodes.StaleGenerated, codes);
    }

    [Fact]
    public async Task A_packet_with_no_eol_attribute_matches_on_its_committed_bytes()
    {
        var codes = await CodesFor(attributes: null, GeneratedSource.Sha256(Encoding.UTF8.GetBytes(PacketLf)));

        Assert.DoesNotContain(DesignDocCodes.StaleGenerated, codes);
    }

    [Fact]
    public async Task A_packet_that_matches_neither_form_still_shows_SND_LIB_017()
    {
        var codes = await CodesFor(Crlf, GeneratedSource.Sha256(Encoding.UTF8.GetBytes("{\"slug\":\"other\"}\n")));

        Assert.Contains(DesignDocCodes.StaleGenerated, codes);
    }

    [Fact]
    public async Task A_bare_clone_resolves_the_eol_attribute_from_the_committed_gitattributes()
    {
        var crlfHash = GeneratedSource.Sha256(Encoding.UTF8.GetBytes(PacketLf.Replace("\n", "\r\n", StringComparison.Ordinal)));
        Commit(Crlf, crlfHash);
        _scratch = Directory.CreateTempSubdirectory("aw-ctx-bare-").FullName;
        var bare = Path.Combine(_scratch, "bare.git");
        _ = _git!.Run("clone", "-q", "--bare", _git.Path, bare);

        var codes = await Pull(new ContextRepo(_git.Repo.Name, bare), new GitCli());

        Assert.DoesNotContain(DesignDocCodes.StaleGenerated, codes);
    }

    [Fact]
    public async Task A_git_without_attr_source_still_hashes_the_checkout_bytes_from_the_working_tree_attributes()
    {
        var crlfHash = GeneratedSource.Sha256(Encoding.UTF8.GetBytes(PacketLf.Replace("\n", "\r\n", StringComparison.Ordinal)));
        Commit(Crlf, crlfHash);
        _scratch = Directory.CreateTempSubdirectory("aw-ctx-oldgit-").FullName;
        var wrapper = ExecutableScript.Write(Path.Combine(_scratch, "old-git"), string.Join('\n',
            "for arg in \"$@\"; do",
            "  case \"$arg\" in",
            "    --attr-source|--attr-source=*) echo 'unknown option: --attr-source' >&2; exit 129 ;;",
            "  esac",
            "done",
            "exec git \"$@\"",
            ""));

        var codes = await Pull(_git!.Repo, new GitCli([wrapper], TimeSpan.FromSeconds(2)));

        Assert.DoesNotContain(DesignDocCodes.StaleGenerated, codes);
    }

    private async Task<IReadOnlySet<string>> CodesFor(string? attributes, string hash)
    {
        Commit(attributes, hash);
        return await Pull(_git!.Repo, new GitCli());
    }

    /// <summary>Commits a generated doc whose source-sha256 is <paramref name="hash"/> beside an LF packet.json (and the .gitattributes, when given), with autocrlf off so only the attribute decides.</summary>
    private void Commit(string? attributes, string hash)
    {
        var fixture = Path.Combine(LibraryFeedRig.RepoRoot(), "tests", "fixtures", "library", "broken", "017-edited-generated");
        var doc = File.ReadAllText(Path.Combine(fixture, "doc.md")).Replace(OldHash, hash, StringComparison.Ordinal);
        var files = new Dictionary<string, string>(StringComparer.Ordinal) { [DocPath] = doc, [PacketPath] = PacketLf };
        if (attributes is not null)
        {
            files["docs/edited/.gitattributes"] = attributes;
        }

        _git = new TempGitRepo(files);
        _ = _git.Run("config", "core.autocrlf", "false");
    }

    /// <summary>Pulls the feed once over <paramref name="repo"/> through <paramref name="git"/>, asserts the doc is an item and returns the codes on it.</summary>
    private async Task<IReadOnlySet<string>> Pull(ContextRepo repo, GitCli git)
    {
        var store = new FakeRecordStore();
        var feed = new RepoDocsFeed([repo], _globs, [], store, git, _scrubbers);
        var page = (await feed.Pull(new RecordPull(After: null, 100, NotBefore: null), Ct)).Value;
        var batch = RecordBatches.From(feed.Source, page, SyncPhase.Incremental, _scrubbers, new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero));
        Assert.Contains(RepoDocs.Key(repo.Name, DocPath), page.Items.Select(static i => i.Key));
        return batch.Records.Select(static w => (w.Side as DesignDocSideRows)?.Doc).OfType<ParsedDesignDoc>().SelectMany(static d => d.Errors).Select(static e => e.Code).ToHashSet(StringComparer.Ordinal);
    }
}
