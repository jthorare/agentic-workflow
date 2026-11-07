using System.Text.Json;
using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Features.ReviewListFacts;
using Aw.Sindri.Tests.Workflow.Blocks.Input;
using RecordKey = Aw.Context.Core.Sources.RecordKey;

namespace Aw.Sindri.Tests.Reviews;

/// <summary>The PR page's stored side over Context (PR review page spec §6.2, §4.4): the PR record, its claims, the claimed doc's items with who else claims them, and the refusal of a repo Sindri doesn't sync.</summary>
[Trait("Lane", "unit")]
public sealed class ContextReviewPageStoreTests
{
    private const string Context = """{"sources":{"github":{"repos":["acme/api"]}}}""";
    private static readonly DateTimeOffset _at = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly RecordKey _doc = RecordKey.From("library:aw/d1");
    private static readonly RecordKey _pr = RecordKey.From("github:pr/acme/api/7");
    private readonly SliceHarness _h = new();
    private readonly StubContext _context = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task Approve()
    {
        var profile = ProfileRules.Check(new SindriProfile(1, "thor", Repos: [new RepoEntry("api", "/repos/api")], Context: JsonDocument.Parse(Context).RootElement.Clone())).Value;
        _ = await _h.Profiles.Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), Ct);
    }

    private ContextReviewPageStore Store()
    {
        var engine = EngineProxy.Of(_context);
        return new ContextReviewPageStore(_h.Profiles, _h.Callers, engine, new ReviewPageLibraryReader(engine, new LibraryHost { Home = "/home/joi", StateDefault = "/home/joi/.agentic-workflow" }));
    }

    private static StoredRecord Record(RecordKey key, RecordState state, params RecordLink[] links) =>
        new(key, SourceId.From("github"), RecordKind.Pr, Repo: null, AudienceKey.From("repo:acme/api"), "Add retry budget", Url: "https://github.com/acme/api/pull/7", Author: "sam-dev", state, StateRaw: "open",
            _at.AddDays(-2), _at, ClosedAt: null, SourceVersion.From("v1"), "hash", Ruleset: "r1", Blocked: false, RemovedAt: null, _at, links);

    private static LibraryItem Item(string key, DesignItemKind kind, string? parent, string text) => new(_doc, key, kind, parent, LineStart: 1, LineEnd: 2, text);

    private static LibraryClaim Claim(RecordKey pr, RecordState state, string reference, LibraryItem item) =>
        new(pr, state, new LibraryRefResolution(reference, LibraryRefStatus.Resolved, _doc, item));

    [Fact]
    public async Task A_repo_outside_the_synced_ones_is_refused_with_the_specs_words()
    {
        await Approve();
        using var caller = _h.Callers.EnterService("page-test");

        var error = (await Store().Read(new ReviewPullRef("acme", "other", 7), Ct)).Error;

        Assert.Equal((SindriErrors.ReviewNotSynced.Code, "Sindri doesn't sync acme/other. Add it to `context.sources.github.repos`."), (error.Code, error.Message));
    }

    [Fact]
    public async Task A_pr_without_a_stored_record_has_no_title_and_an_empty_library()
    {
        await Approve();
        using var caller = _h.Callers.EnterService("page-test");

        var stored = (await Store().Read(new ReviewPullRef("acme", "api", 7), Ct)).Value;

        Assert.Null(stored.Title);
        Assert.Empty(stored.Library.Claims);
    }

    [Fact]
    public async Task The_record_gives_its_header_facts_and_the_linear_chips_its_links_name()
    {
        await Approve();
        using var caller = _h.Callers.EnterService("page-test");
        _context.Pulls = [Record(_pr, RecordState.Draft, new RecordLink("linear:issue-key/ENG-12", RecordLinkKind.Relates, Title: null), new RecordLink("https://example.test/x", RecordLinkKind.Relates, Title: null))];

        var stored = (await Store().Read(new ReviewPullRef("acme", "api", 7), Ct)).Value;

        Assert.Equal(("Add retry budget", "sam-dev", true, "https://github.com/acme/api/pull/7"), (stored.Title, stored.Author, stored.Draft, stored.Url));
        Assert.Equal(["ENG-12"], [.. stored.IssueKeys]);
    }

    [Fact]
    public async Task The_claimed_docs_items_carry_who_else_claims_them_and_the_test_rows_that_verify_a_claimed_item()
    {
        await Approve();
        using var caller = _h.Callers.EnterService("page-test");
        var ph4 = Item("Ph4", DesignItemKind.Phase, parent: null, "Settings");
        var ph5 = Item("Ph5", DesignItemKind.Phase, parent: null, "Routes");
        var req = Item("REQ-E3", DesignItemKind.Requirement, "Ph5", "Every route answers");
        var section = Item("sec", DesignItemKind.Section, "Ph5", "A section between");
        var tp = Item("TP3", DesignItemKind.TestPlan, parent: null, "GET answers every panel (verifies REQ-E3)");
        _context.Pulls = [Record(_pr, RecordState.Open)];
        _context.PrClaims = [Claim(_pr, RecordState.Open, "d1#Ph5", ph5), Claim(_pr, RecordState.Open, "d1#REQ-E3", req)];
        _context.DocItems = [ph4, ph5, section, req, tp];
        _context.OnDoc = [Claim(RecordKey.From("github:pr/acme/api/6"), RecordState.Merged, "d1#Ph4", ph4), Claim(_pr, RecordState.Open, "d1#Ph5", ph5)];

        var library = (await Store().Read(new ReviewPullRef("acme", "api", 7), Ct)).Value.Library;

        Assert.Equal(["d1#Ph5", "d1#REQ-E3"], [.. library.Claims.Select(static c => c.Ref)]);
        var items = library.Doc!.Items;
        Assert.Equal(["Ph4", "Ph5", "REQ-E3"], [.. items.Select(static i => i.Key)]);
        Assert.Equal(("claimed by #6, merged", true), (Assert.Single(items[0].ClaimedBy).Text, items[1].ClaimedHere));
        Assert.Equal("Ph5", items[2].Parent);   // the section between them is not a node
        Assert.Equal(["TP3"], [.. library.TestRows.Select(static r => r.Key)]);
        Assert.Equal(["REQ-E3"], [.. library.TestRows[0].Verifies]);
    }

    private sealed class StubContext : IContextPulls, IContextLibrary
    {
        public IReadOnlyList<StoredRecord> Pulls { get; set; } = [];

        public IReadOnlyList<LibraryClaim> PrClaims { get; set; } = [];

        public IReadOnlyList<LibraryItem> DocItems { get; set; } = [];

        public IReadOnlyList<LibraryClaim> OnDoc { get; set; } = [];

        public ValueTask<Result<IReadOnlyList<StoredRecord>>> OpenPulls(AudienceScope audience, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(Pulls));

        public ValueTask<Result<IReadOnlyList<LibraryClaim>>> Claims(RecordKey prKey, AudienceScope audience, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(PrClaims));

        public ValueTask<Result<IReadOnlyDictionary<RecordKey, IReadOnlyList<LibraryClaim>>>> Claims(IReadOnlyList<RecordKey> prKeys, AudienceScope audience, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success<IReadOnlyDictionary<RecordKey, IReadOnlyList<LibraryClaim>>>(new Dictionary<RecordKey, IReadOnlyList<LibraryClaim>>()));

        public ValueTask<Result<IReadOnlyList<LibraryClaim>>> ClaimsOn(RecordKey docKey, AudienceScope audience, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(OnDoc));

        public ValueTask<Result<IReadOnlyList<LibraryItem>>> Items(RecordKey docKey, AudienceScope audience, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(DocItems));

        public ValueTask<Result<LibraryDoc?>> GetDoc(RecordKey key, AudienceScope audience, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success<LibraryDoc?>(new LibraryDoc(key, "aw", "aw/acme-api", "d1", "spec", "approved", Project: null, ["acme/api"], Issues: [], ParserVersion: 1, Errors: [], "The design doc", Url: null, AudienceKey.From("repo:acme/api"))));

        public ValueTask<Result<IReadOnlyList<LibraryDoc>>> ListDocs(LibraryDocFilter filter, AudienceScope audience, CancellationToken cancellationToken) => FakeContextEngine.Instance.ListDocs(filter, audience, cancellationToken);

        public ValueTask<Result<IReadOnlyList<LibraryRefResolution>>> Resolve(IReadOnlyList<string> refs, string? repo, AudienceScope audience, CancellationToken cancellationToken) => FakeContextEngine.Instance.Resolve(refs, repo, audience, cancellationToken);

        public ValueTask<Result<IReadOnlyList<LibraryRootStatus>>> Roots(ContextScope scope, CancellationToken cancellationToken) => FakeContextEngine.Instance.Roots(scope, cancellationToken);
    }
}
