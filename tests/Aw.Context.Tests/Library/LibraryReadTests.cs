using Aw.Context.Core.Audience;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Features.ReadLibrary;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Infrastructure;

namespace Aw.Context.Tests.Library;

/// <summary>What <c language="text">IContextLibrary</c> reads from the stored design-doc rows, through the caller's audience (PR review page spec 2026-11-04 §5.4, §5.5, §4.4).</summary>
public sealed class LibraryReadTests : IDisposable
{
    private const string Plan = "## Goals\n- **REQ-1** Book a ferry.\n- **REQ-2** Cancel a booking.\n| **TP-1** | Booking works. Verifies REQ-1 |\n## Phases\n- **Ph1** Booking.\n";
    private const string Secret = "## Goals\n- **REQ-9** Keep it quiet.\n";
    private const string RepoDoc = "repo:widgets/docs/harbor.md";
    private const string OwnerDoc = "doc:aw/slug/plans/private.md";
    private const string Pr = "github:pr/acme/widgets/12";
    private const string OwnerPr = "github:pr/acme/widgets/13";

    private static readonly SourceId _library = SourceId.From("library");
    private static readonly AudienceKey _repoAudience = AudienceKey.From("repo:acme/widgets");
    private static readonly AudienceKey _ownerAudience = AudienceKey.From("owner:library");

    private readonly RecordStoreWorld _world = new();
    private readonly Dictionary<SourceId, int> _pages = [];

    public void Dispose() => _world.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AudienceScope Session(params RecordKey[] linked) => new(new HashSet<AudienceKey> { _repoAudience }) { Records = linked.ToHashSet() };

    private static AudienceScope Owner() => new(new HashSet<AudienceKey> { _repoAudience, _ownerAudience });

    private ContextLibrary Library => new(_world.Library);

    private static ParsedDesignDoc Parsed(string slug, string body, string repos = "") =>
        DesignDocParser.Parse("/vault/" + slug + ".md", ADoc.Front("spec", slug, repos.Length == 0 ? string.Empty : $"repos: [{repos}]\n") + body);

    private async Task PutDoc(SourceId source, string key, AudienceKey audience, ParsedDesignDoc parsed, string version = "v1")
    {
        var record = RecordSamples.Record(key, version, source) with { Kind = RecordKind.Doc, Audience = audience };
        await Commit(source, RecordSamples.Write(record, "words") with { Side = new DesignDocSideRows(parsed) });
    }

    private async Task PutPr(string key, AudienceKey audience, string[] claims, RecordState state = RecordState.Open)
    {
        var record = RecordSamples.Record(key, "v1", RecordSamples.Github) with { Kind = RecordKind.Pr, Audience = audience, State = state };
        await Commit(RecordSamples.Github, RecordSamples.Write(record, "words") with { Side = new PrClaimSideRows(claims) });
    }

    private async Task Remove(SourceId source, string key)
    {
        var page = _pages[source] = _pages.GetValueOrDefault(source) + 1;
        _ = (await _world.Store.Commit(RecordSamples.Batch(source, page) with { Removed = [RecordKey.From(key)] }, Ct)).Value;
    }

    private async Task Commit(SourceId source, RecordWrite write)
    {
        var page = _pages[source] = _pages.GetValueOrDefault(source) + 1;
        _ = (await _world.Store.Commit(RecordSamples.Batch(source, page, write), Ct)).Value;
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_session_lists_gets_and_reads_items_of_its_own_audience_only_and_the_owner_reads_the_owner_only_doc_too()
    {
        await PutDoc(RepoDocs.Id, RepoDoc, _repoAudience, Parsed("harbor", Plan));
        await PutDoc(_library, OwnerDoc, _ownerAudience, Parsed("private-plan", Secret));
        var session = Session(RecordKey.From(OwnerDoc));   // rule (c) lists the owner doc; it must not let it through
        var all = new LibraryDocFilter();

        Assert.Equal([RepoDoc], (await Library.ListDocs(all, session, Ct)).Value.Select(static d => d.Key.Value), StringComparer.Ordinal);
        Assert.Equal([OwnerDoc, RepoDoc], (await Library.ListDocs(all, Owner(), Ct)).Value.Select(static d => d.Key.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.NotNull((await Library.GetDoc(RecordKey.From(RepoDoc), session, Ct)).Value);
        Assert.Null((await Library.GetDoc(RecordKey.From(OwnerDoc), session, Ct)).Value);
        Assert.Equal("private-plan", (await Library.GetDoc(RecordKey.From(OwnerDoc), Owner(), Ct)).Value?.Slug);
        Assert.Equal(["goals", "REQ-1", "REQ-2", "TP-1", "phases", "Ph1"], (await Library.Items(RecordKey.From(RepoDoc), session, Ct)).Value.Select(static i => i.Key), StringComparer.Ordinal);
        Assert.Empty((await Library.Items(RecordKey.From(OwnerDoc), session, Ct)).Value);
        Assert.Equal(["goals", "REQ-9"], (await Library.Items(RecordKey.From(OwnerDoc), Owner(), Ct)).Value.Select(static i => i.Key), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_listing_filters_by_root_project_repo_and_errors()
    {
        await PutDoc(RepoDocs.Id, RepoDoc, _repoAudience, Parsed("harbor", Plan));
        await PutDoc(_library, OwnerDoc, _repoAudience, Parsed("private-plan", Secret, repos: "acme/widgets"));
        await PutDoc(_library, "doc:work/broken.md", _repoAudience, Parsed("broken-doc", "## Goals\n- **REQ-** No number.\n"));

        async Task<string[]> Keys(LibraryDocFilter filter) => [.. (await Library.ListDocs(filter, Session(), Ct)).Value.Select(static d => d.Key.Value).Order(StringComparer.Ordinal)];

        Assert.Equal(3, (await Keys(new LibraryDocFilter())).Length);
        Assert.Equal([RepoDoc], await Keys(new LibraryDocFilter(Root: "repo:widgets")), StringComparer.Ordinal);
        Assert.Equal([OwnerDoc, RepoDoc], await Keys(new LibraryDocFilter(Repo: "acme/widgets")), StringComparer.Ordinal);   // the doc that names the repo, and the doc committed to it
        Assert.Equal(["doc:work/broken.md"], await Keys(new LibraryDocFilter(ErrorsOnly: true)), StringComparer.Ordinal);
        Assert.Empty(await Keys(new LibraryDocFilter(Project: "nothing")));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_claim_is_read_through_its_pull_requests_audience_and_its_docs()
    {
        await PutDoc(RepoDocs.Id, RepoDoc, _repoAudience, Parsed("harbor", Plan));
        await PutDoc(_library, OwnerDoc, _ownerAudience, Parsed("private-plan", Secret));
        await PutPr(Pr, _repoAudience, ["harbor#REQ-1", "doc:aw/slug/private-plan#REQ-9"]);
        await PutPr(OwnerPr, _ownerAudience, ["harbor#REQ-2"]);

        var session = (await Library.Claims(RecordKey.From(Pr), Session(RecordKey.From(OwnerDoc)), Ct)).Value;
        var owner = (await Library.Claims(RecordKey.From(Pr), Owner(), Ct)).Value;

        Assert.Equal([LibraryRefStatus.Missing, LibraryRefStatus.Resolved], session.OrderBy(static c => c.Resolution.Ref, StringComparer.Ordinal).Select(static c => c.Resolution.Status));   // the owner-only doc is missing to a session
        Assert.Equal([LibraryRefStatus.Resolved, LibraryRefStatus.Resolved], owner.Select(static c => c.Resolution.Status));
        Assert.Empty((await Library.Claims(RecordKey.From(OwnerPr), Session(), Ct)).Value);   // the owner-only PR's half
        _ = Assert.Single((await Library.Claims(RecordKey.From(OwnerPr), Owner(), Ct)).Value);

        Assert.Equal([Pr], (await Library.ClaimsOn(RecordKey.From(RepoDoc), Session(), Ct)).Value.Select(static c => c.Pr.Value), StringComparer.Ordinal);
        Assert.Equal([Pr, OwnerPr], (await Library.ClaimsOn(RecordKey.From(RepoDoc), Owner(), Ct)).Value.Select(static c => c.Pr.Value), StringComparer.Ordinal);
        Assert.Empty((await Library.ClaimsOn(RecordKey.From(OwnerDoc), Session(RecordKey.From(OwnerDoc)), Ct)).Value);
        Assert.Equal([Pr], (await Library.ClaimsOn(RecordKey.From(OwnerDoc), Owner(), Ct)).Value.Select(static c => c.Pr.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_store_reads_the_claims_of_many_PRs_in_one_call_in_PR_then_ref_order()
    {
        const string Second = "github:pr/acme/widgets/14";
        const string Bare = "github:pr/acme/widgets/15";
        await PutPr(Second, _repoAudience, ["harbor#REQ-2"]);
        await PutPr(Pr, _repoAudience, ["harbor#REQ-2", "harbor#REQ-1"]);
        await PutPr(Bare, _repoAudience, []);

        var rows = (await _world.Library.ClaimsOfMany([RecordKey.From(Second), RecordKey.From(Pr), RecordKey.From(Bare)], Ct)).Value;

        Assert.Equal([(Pr, "harbor#REQ-1"), (Pr, "harbor#REQ-2"), (Second, "harbor#REQ-2")], rows.Select(static r => (r.Pr.Value, r.Ref)));
        Assert.Empty((await _world.Library.ClaimsOfMany([RecordKey.From(Bare)], Ct)).Value);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Claims_for_many_PRs_leave_out_a_PR_outside_the_audience_and_map_a_claimless_PR_to_an_empty_list()
    {
        const string Bare = "github:pr/acme/widgets/15";
        await PutDoc(RepoDocs.Id, RepoDoc, _repoAudience, Parsed("harbor", Plan));
        await PutPr(Pr, _repoAudience, ["harbor#REQ-1"]);
        await PutPr(OwnerPr, _ownerAudience, ["harbor#REQ-2"]);
        await PutPr(Bare, _repoAudience, []);
        RecordKey[] asked = [RecordKey.From(Pr), RecordKey.From(OwnerPr), RecordKey.From(Bare)];

        var session = (await Library.Claims(asked, Session(), Ct)).Value;
        var owner = (await Library.Claims(asked, Owner(), Ct)).Value;

        Assert.Equal([Pr, Bare], session.Keys.Select(static k => k.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);   // the owner-only PR has no entry
        Assert.Equal(LibraryRefStatus.Resolved, Assert.Single(session[RecordKey.From(Pr)]).Resolution.Status);
        Assert.Empty(session[RecordKey.From(Bare)]);
        Assert.Equal(3, owner.Count);
        _ = Assert.Single(owner[RecordKey.From(OwnerPr)]);
        Assert.Empty((await Library.Claims([], Owner(), Ct)).Value);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_claim_written_before_its_doc_resolves_once_the_doc_lands()
    {
        await PutPr(Pr, _repoAudience, ["harbor#REQ-1"]);
        var before = (await Library.Claims(RecordKey.From(Pr), Owner(), Ct)).Value;
        Assert.Equal(LibraryRefStatus.Missing, Assert.Single(before).Resolution.Status);

        await PutDoc(RepoDocs.Id, RepoDoc, _repoAudience, Parsed("harbor", Plan));

        var after = Assert.Single((await Library.Claims(RecordKey.From(Pr), Owner(), Ct)).Value);
        Assert.Equal((LibraryRefStatus.Resolved, RepoDoc, "REQ-1"), (after.Resolution.Status, after.Resolution.Doc?.Value, after.Resolution.Item?.Key));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_removed_docs_rows_go_and_its_id_passes_to_the_next_doc()
    {
        const string First = "repo:widgets/docs/a.md";
        const string Second = "repo:widgets/docs/b.md";
        await PutDoc(RepoDocs.Id, First, _repoAudience, Parsed("harbor", "## Goals\n- **REQ-1** First.\n"));
        await PutDoc(RepoDocs.Id, Second, _repoAudience, Parsed("harbor", "## Goals\n- **REQ-7** Second.\n"));

        var before = (await Library.ListDocs(new LibraryDocFilter(), Owner(), Ct)).Value.ToDictionary(static d => d.Key.Value, StringComparer.Ordinal);
        Assert.Empty(before[First].Errors);
        Assert.Equal([DesignDocCodes.SharedDocId], before[Second].Errors.Select(static e => e.Code), StringComparer.Ordinal);
        var firstTry = (await Library.Resolve(["harbor#REQ-1", "harbor#REQ-7"], "acme/widgets", Owner(), Ct)).Value.Select(static r => r.Status);
        Assert.Equal([LibraryRefStatus.Resolved, LibraryRefStatus.Missing], firstTry);

        await Remove(RepoDocs.Id, First);

        Assert.Equal(0, _world.Count($"SELECT count(*) FROM design_doc WHERE doc_key = '{First}'") + _world.Count($"SELECT count(*) FROM design_doc_item WHERE doc_key = '{First}'"));
        var after = (await Library.ListDocs(new LibraryDocFilter(), Owner(), Ct)).Value;
        Assert.Equal([Second], after.Select(static d => d.Key.Value), StringComparer.Ordinal);
        Assert.Empty(after[0].Errors);
        var secondTry = (await Library.Resolve(["harbor#REQ-1", "harbor#REQ-7"], "acme/widgets", Owner(), Ct)).Value.Select(static r => r.Status);
        Assert.Equal([LibraryRefStatus.Missing, LibraryRefStatus.Resolved], secondTry);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_short_ref_looks_in_the_repos_own_root_then_in_docs_that_name_the_repo_and_two_of_those_are_ambiguous()
    {
        await PutDoc(_library, "doc:aw/one/plans/a.md", _repoAudience, Parsed("shared-plan", Secret, repos: "acme/widgets"));
        await PutDoc(_library, "doc:aw/two/plans/a.md", _repoAudience, Parsed("shared-plan", Secret, repos: "acme/widgets"));
        await PutDoc(_library, "doc:aw/three/plans/a.md", _repoAudience, Parsed("only-here", Secret, repos: "acme/widgets"));

        var resolved = (await Library.Resolve(["shared-plan#REQ-9", "only-here#REQ-9", "doc:aw/one/shared-plan#REQ-9", "only-here#REQ-9"], "acme/widgets", Owner(), Ct)).Value;
        var elsewhere = (await Library.Resolve(["only-here#REQ-9"], "acme/other", Owner(), Ct)).Value;

        Assert.Equal([LibraryRefStatus.Ambiguous, LibraryRefStatus.Resolved, LibraryRefStatus.Resolved, LibraryRefStatus.Resolved], resolved.Select(static r => r.Status));
        Assert.Equal(LibraryRefStatus.Missing, Assert.Single(elsewhere).Status);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_phase_ref_with_leading_zeros_names_the_phase_and_a_claim_of_a_closed_pull_request_is_not_shown_on_the_doc()
    {
        await PutDoc(RepoDocs.Id, RepoDoc, _repoAudience, Parsed("harbor", Plan));
        await PutPr(Pr, _repoAudience, ["harbor#Ph01"], RecordState.Merged);
        await PutPr(OwnerPr, _repoAudience, ["harbor#Ph1"], RecordState.Closed);

        var onDoc = (await Library.ClaimsOn(RecordKey.From(RepoDoc), Owner(), Ct)).Value;
        var closed = (await Library.Claims(RecordKey.From(OwnerPr), Owner(), Ct)).Value;

        var shown = Assert.Single(onDoc);
        Assert.Equal((Pr, RecordState.Merged, "Ph1"), (shown.Pr.Value, shown.PrState, shown.Resolution.Item?.Key));
        Assert.Equal(LibraryRefStatus.Resolved, Assert.Single(closed).Resolution.Status);   // the PR's own claims are still readable
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_sub_phase_ref_with_leading_zeros_names_the_sub_phase()
    {
        await PutDoc(RepoDocs.Id, RepoDoc, _repoAudience, Parsed("harbor", "## Phases\n- **Ph2-1** Sub step.\n"));

        var both = (await Library.Resolve(["harbor#Ph2-01", "harbor#Ph02-01"], "acme/widgets", Owner(), Ct)).Value;

        Assert.Equal(["Ph2-1", "Ph2-1"], both.Select(static r => r.Item?.Key), StringComparer.Ordinal);
        Assert.All(both, static r => Assert.Equal(LibraryRefStatus.Resolved, r.Status));
    }
}
