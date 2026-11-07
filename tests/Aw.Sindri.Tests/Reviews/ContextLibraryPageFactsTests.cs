using System.Text.Json;
using Aw.Context.Core.Audience;
using Aw.Context.Core.ConceptSynthesis;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Features.ReviewListFacts;
using Aw.Sindri.Tests.Workflow.Blocks.Input;
using RecordKey = Aw.Context.Core.Sources.RecordKey;

namespace Aw.Sindri.Tests.Reviews;

/// <summary>The library page's roots over Context (Settings → Library): the aw root and vaults from the feed's root states, the repo roots from the profile's repos, counts and scan times from stored data.</summary>
[Trait("Lane", "unit")]
public sealed class ContextLibraryPageFactsTests
{
    private const string Context = """{"sources":{"github":{"repos":["acme/api"]},"library":{"vaults":[{"name":"work","path":"~/Documents/Work","deny":["Private/**"]}]}}}""";
    private static readonly DateTimeOffset _at = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly LibraryHost _host = new() { Home = "/home/joi", StateDefault = "/home/joi/.agentic-workflow" };
    private readonly SliceHarness _h = new();
    private readonly Stub _context = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task Approve()
    {
        var profile = ProfileRules.Check(new SindriProfile(1, "thor", Repos: [new RepoEntry("api", "/home/joi/repos/api"), new RepoEntry("web", "/srv/web")], Context: JsonDocument.Parse(Context).RootElement.Clone())).Value;
        _ = await _h.Profiles.Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), Ct);
    }

    private ContextLibraryPageFacts Facts() => new(_h.Profiles, _h.Callers, EngineProxy.Of(_context), _host);

    private static LibraryDoc Doc(string root, string file, int errors) =>
        new(RecordKey.From($"doc:{root}/{file}"), root, root, Slug: file, Kind: null, Status: null, Project: null, [], [], ParserVersion: 1,
            [.. Enumerable.Range(0, errors).Select(static _ => new DesignDocError("SND-LIB-010", Line: 3, "m", "f"))], file, Url: null, Audience: null);

    private static LibraryRootStatus Status(string root, LibraryRootState state, string? reason, int docs) => new(root, state, reason, docs, NearMisses: 0, _at);

    [Fact]
    public async Task A_human_gets_the_aw_root_then_the_vaults_then_a_repo_root_for_each_profile_repo_with_its_counts()
    {
        await Approve();
        using var caller = _h.Callers.EnterService("library-test");
        _context.Statuses = [Status("aw", LibraryRootState.Ok, reason: null, docs: 9), Status("work", LibraryRootState.Ok, reason: null, docs: 20)];
        _context.Docs = [Doc("work", "a.md", 2), Doc("work", "b.md", 0), Doc("repo:api", "docs/x.md", 1), Doc("repo:api", "docs/y.md", 0)];
        _context.Progress = [new SourceProgress(SourceId.From("repo-docs"), Phase: null, _at.AddMinutes(1), LastCode: null, Usable: true, Complete: true)];

        var roots = (await Facts().Read(Ct)).Value.Roots;

        Assert.Equal(
            [
                ("aw", LibraryRootKind.Aw, "~/.agentic-workflow", null, 9, 0, _at),
                ("work", LibraryRootKind.Vault, "~/Documents/Work", null, 20, 1, _at),
                ("repo:acme/api", LibraryRootKind.Repo, "~/repos/api", "acme/api", 2, 1, _at.AddMinutes(1)),
                ("repo:web", LibraryRootKind.Repo, "/srv/web", "web", 0, 0, _at.AddMinutes(1)),
            ],
            roots.Select(static r => (r.Name, r.Kind, r.Path, r.Repo, r.Docs, r.Errors, r.ScannedAt)));
        Assert.Equal(["Private/**"], roots[1].Deny);
    }

    [Fact]
    public async Task A_root_that_cannot_be_read_carries_its_reason_and_a_repo_root_before_the_first_sync_has_no_scan_time()
    {
        await Approve();
        using var caller = _h.Callers.EnterService("library-test");
        _context.Statuses = [Status("aw", LibraryRootState.Ok, reason: null, docs: 0), Status("work", LibraryRootState.Unreadable, "permission denied", docs: 0)];

        var roots = (await Facts().Read(Ct)).Value.Roots;

        Assert.Equal("permission denied", roots[1].Unreadable);
        Assert.Null(roots[2].ScannedAt);
    }

    [Fact]
    public async Task A_session_gets_no_roots()
    {
        await Approve();
        using var caller = _h.Callers.Enter(new CallerContext(CallerClass.Session, Terminal: false, "session:mcp", _h.Ids.New<CommandId>(), _h.Ids.New<CorrelationId>(), RepoName.From("api")));
        _context.Statuses = [Status("aw", LibraryRootState.Ok, reason: null, docs: 9)];

        var facts = (await Facts().Read(Ct)).Value;

        Assert.Empty(facts.Roots);
    }

    /// <summary>Answers the library reads and the sync progress as set.</summary>
    private sealed class Stub : IContextLibrary, IIndexSync
    {
        public IReadOnlyList<LibraryDoc> Docs { get; set; } = [];

        public IReadOnlyList<LibraryRootStatus> Statuses { get; set; } = [];

        public IReadOnlyList<SourceProgress> Progress { get; set; } = [];

        public ValueTask<Result<IReadOnlyList<LibraryDoc>>> ListDocs(LibraryDocFilter filter, AudienceScope audience, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(Docs));

        ValueTask<Result<IReadOnlyList<LibraryRootStatus>>> IContextLibrary.Roots(ContextScope scope, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(Statuses));

        ValueTask<Result<IReadOnlyList<SourceProgress>>> IIndexSync.Progress(ContextScope scope, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(Progress));

        public ValueTask<Result<LibraryDoc?>> GetDoc(RecordKey key, AudienceScope audience, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LibraryItem>>> Items(RecordKey docKey, AudienceScope audience, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LibraryRefResolution>>> Resolve(IReadOnlyList<string> refs, string? repo, AudienceScope audience, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LibraryClaim>>> Claims(RecordKey prKey, AudienceScope audience, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LibraryClaim>>> ClaimsOn(RecordKey docKey, AudienceScope audience, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<SyncReport>> Tick(ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<SyncReport>> Tick(ContextScope scope, SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<DecisionReport>> Decide(ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<ConceptStatus>> ConceptStatus(ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<SourceCounts>> Counts(SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<int>> Purge(SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
