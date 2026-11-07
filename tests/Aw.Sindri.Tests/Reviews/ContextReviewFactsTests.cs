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
using Aw.Context.Testing.Engine;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.ReviewListFacts;
using Aw.Sindri.Tests.Workflow.Blocks.Input;
using RecordKey = Aw.Context.Core.Sources.RecordKey;

namespace Aw.Sindri.Tests.Reviews;

/// <summary>The Reviews list's facts over Context (PR review page spec §6.1, §5.6): stored PR records of the synced repos, the caller's audiences, the github source's state and the library header.</summary>
[Trait("Lane", "unit")]
public sealed class ContextReviewFactsTests
{
    private const string Context = """{"sources":{"github":{"repos":["acme/api","acme/web"]}}}""";
    private static readonly DateTimeOffset _at = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    private readonly SliceHarness _h = new();
    private readonly StubContext _context = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task Approve()
    {
        var profile = ProfileRules.Check(new SindriProfile(1, "thor", Repos: [new RepoEntry("api", "/repos/api"), new RepoEntry("web", "/repos/web")], Context: JsonDocument.Parse(Context).RootElement.Clone())).Value;
        _ = await _h.Profiles.Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), Ct);
    }

    private ContextReviewFacts Facts() => new(_h.Profiles, _h.Callers, EngineProxy.Of(_context), new ContextLibraryState(EngineProxy.Of(_context)));

    private static StoredRecord Pull(string key, RecordState state, PullFacts? facts = null) =>
        new(RecordKey.From(key), SourceId.From("github"), RecordKind.Pr, Repo: null, AudienceKey.From("repo:acme/api"), $"Title of {key}", Url: "https://github.com/" + key, Author: "sam-dev", state, StateRaw: "open",
            _at.AddDays(-2), _at, ClosedAt: null, SourceVersion.From("v1"), "hash", Ruleset: "r1", Blocked: false, RemovedAt: null, _at, Links: [], Pull: facts);

    private static SourceProgress Source(string id, bool usable, string? code) => new(SourceId.From(id), Phase: null, _at, code, usable, Complete: usable);

    [Fact]
    public async Task A_human_reads_the_open_and_draft_PRs_of_the_synced_repos_with_their_reviewers()
    {
        await Approve();
        using var caller = _h.Callers.EnterService("reviews-test");
        _context.Pulls =
        [
            Pull("github:pr/acme/api/7", RecordState.Open, new PullFacts(["joi-example"], "abc123", [new ReviewedCommit("kim-dev", "def456", NewCommits: 2)])),
            Pull("github:pr/acme/web/3", RecordState.Draft),
            Pull("github:pr/acme/lib/9", RecordState.Open),          // not a synced repo
            Pull("github:issue/acme/api/5", RecordState.Open),       // not a PR key
            Pull("github:pr/acme/api/notanumber", RecordState.Open),
        ];
        _context.Progress = [Source("github", usable: true, code: null)];

        var snapshot = (await Facts().Read(Ct)).Value;

        Assert.Equal(["acme/api#7", "acme/web#3"], snapshot.Pulls.Select(static p => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{p.Repo}#{p.Number}")), StringComparer.Ordinal);
        var api = snapshot.Pulls[0];
        Assert.Equal(("Title of github:pr/acme/api/7", "sam-dev", false, "abc123", true, "https://github.com/github:pr/acme/api/7"), (api.Title, api.Author, api.Draft, api.HeadSha, api.ReviewersRead, api.Url));
        Assert.Equal(["joi-example"], api.RequestedReviewers, StringComparer.Ordinal);
        Assert.Equal(("kim-dev", "def456", 2), (api.Reviews[0].Login, api.Reviews[0].Commit, api.Reviews[0].NewCommits));
        var web = snapshot.Pulls[1];
        Assert.True(web.Draft);
        Assert.False(web.ReviewersRead);
        Assert.Equal(("github", true, null), (snapshot.Github!.Source, snapshot.Github.Usable, snapshot.Github.LastCode));
        Assert.Equal(["repo:acme/api", "repo:acme/web"], _context.AskedAudience!.Allowed.Select(static k => k.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_list_load_asks_for_the_claims_of_all_its_PRs_in_one_call()
    {
        await Approve();
        using var caller = _h.Callers.EnterService("reviews-test");
        _context.Pulls = [Pull("github:pr/acme/api/1", RecordState.Open), Pull("github:pr/acme/api/2", RecordState.Open), Pull("github:pr/acme/web/3", RecordState.Draft), Pull("github:pr/acme/web/4", RecordState.Open)];
        _context.Progress = [Source("github", usable: true, code: null)];

        var snapshot = (await Facts().Read(Ct)).Value;

        Assert.Equal(4, snapshot.Pulls.Count);
        Assert.True(_context.ClaimsCalls == 1, $"#2154: the claims of 4 open PRs took {_context.ClaimsCalls.ToString(System.Globalization.CultureInfo.InvariantCulture)} library calls, expected one batched call");
    }

    [Fact]
    public async Task A_session_reads_only_its_own_repos_audience()
    {
        await Approve();
        using var caller = _h.Callers.Enter(new CallerContext(CallerClass.Session, Terminal: false, "session:mcp", _h.Ids.New<CommandId>(), _h.Ids.New<CorrelationId>(), RepoName.From("api")));

        _ = await Facts().Read(Ct);

        Assert.Equal(["repo:acme/api"], _context.AskedAudience!.Allowed.Select(static k => k.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Without_a_caller_the_read_is_refused()
    {
        await Approve();

        var read = await Facts().Read(Ct);

        Assert.Equal(SindriErrors.AuthRefused.Code, read.Error.Code);
    }

    [Fact]
    public async Task Without_an_approved_profile_the_read_is_SND_PROFILE_005()
    {
        using var caller = _h.Callers.EnterService("reviews-test");

        var read = await Facts().Read(Ct);

        Assert.Equal(SindriErrors.ProfileNotApproved.Code, read.Error.Code);
    }

    [Fact]
    public async Task A_failing_progress_read_is_the_error()
    {
        await Approve();
        using var caller = _h.Callers.EnterService("reviews-test");
        _context.Failure = new Error("SND-CONTEXT-004", ErrorKind.Unavailable, "the store is not open");

        var read = await Facts().Read(Ct);

        Assert.Equal("SND-CONTEXT-004", read.Error.Code);
    }

    [Theory]
    [InlineData(false, null, true)]          // not usable and never synced: the first read is still running
    [InlineData(false, "SND-INDEX-401", false)]
    [InlineData(true, null, false)]
    public async Task The_library_header_is_reading_only_until_the_first_library_sync_has_run(bool usable, string? code, bool reading)
    {
        await Approve();
        using var caller = _h.Callers.EnterService("reviews-test");
        _context.Progress = [Source("library", usable, code)];

        var library = (await Facts().Read(Ct)).Value.Library;

        Assert.Equal((reading, 0, 0, 0), (library.Reading, library.Docs, library.WithErrors, library.UnreadableRoots.Count));
    }

    /// <summary>Answers OpenPulls and Progress as set; every other call falls through to the fake engine.</summary>
    private sealed class StubContext : IContextPulls, IIndexSync, IContextLibrary
    {
        public IReadOnlyList<StoredRecord> Pulls { get; set; } = [];

        public IReadOnlyList<SourceProgress> Progress { get; set; } = [];

        public Error? Failure { get; set; }

        public AudienceScope? AskedAudience { get; private set; }

        /// <summary>How many times the facts asked for a PR's claims (#2154).</summary>
        public int ClaimsCalls { get; private set; }

        public ValueTask<Result<IReadOnlyDictionary<RecordKey, IReadOnlyList<LibraryClaim>>>> Claims(IReadOnlyList<RecordKey> prKeys, AudienceScope audience, CancellationToken cancellationToken)
        {
            ClaimsCalls++;
            return ValueTask.FromResult(Results.Success<IReadOnlyDictionary<RecordKey, IReadOnlyList<LibraryClaim>>>(new Dictionary<RecordKey, IReadOnlyList<LibraryClaim>>()));
        }

        public ValueTask<Result<IReadOnlyList<LibraryClaim>>> Claims(RecordKey prKey, AudienceScope audience, CancellationToken cancellationToken)
        {
            ClaimsCalls++;
            return ValueTask.FromResult(Results.Success<IReadOnlyList<LibraryClaim>>([]));
        }

        public ValueTask<Result<IReadOnlyList<LibraryDoc>>> ListDocs(LibraryDocFilter filter, AudienceScope audience, CancellationToken cancellationToken) => FakeContextEngine.Instance.ListDocs(filter, audience, cancellationToken);

        public ValueTask<Result<LibraryDoc?>> GetDoc(RecordKey key, AudienceScope audience, CancellationToken cancellationToken) => FakeContextEngine.Instance.GetDoc(key, audience, cancellationToken);

        public ValueTask<Result<IReadOnlyList<LibraryItem>>> Items(RecordKey docKey, AudienceScope audience, CancellationToken cancellationToken) => FakeContextEngine.Instance.Items(docKey, audience, cancellationToken);

        public ValueTask<Result<IReadOnlyList<LibraryRefResolution>>> Resolve(IReadOnlyList<string> refs, string? repo, AudienceScope audience, CancellationToken cancellationToken) => FakeContextEngine.Instance.Resolve(refs, repo, audience, cancellationToken);

        public ValueTask<Result<IReadOnlyList<LibraryClaim>>> ClaimsOn(RecordKey docKey, AudienceScope audience, CancellationToken cancellationToken) => FakeContextEngine.Instance.ClaimsOn(docKey, audience, cancellationToken);

        public ValueTask<Result<IReadOnlyList<LibraryRootStatus>>> Roots(ContextScope scope, CancellationToken cancellationToken) => FakeContextEngine.Instance.Roots(scope, cancellationToken);

        public ValueTask<Result<IReadOnlyList<StoredRecord>>> OpenPulls(AudienceScope audience, CancellationToken cancellationToken)
        {
            AskedAudience = audience;
            return ValueTask.FromResult(Results.Success(Pulls));
        }

        ValueTask<Result<IReadOnlyList<SourceProgress>>> IIndexSync.Progress(ContextScope scope, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Failure is { } f ? (Result<IReadOnlyList<SourceProgress>>)f : Results.Success(Progress));

        public ValueTask<Result<SyncReport>> Tick(ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<SyncReport>> Tick(ContextScope scope, SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<DecisionReport>> Decide(ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<ConceptStatus>> ConceptStatus(ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<SourceCounts>> Counts(SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<int>> Purge(SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
