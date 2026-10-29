using Aw.Context.Core.Linear;
using Aw.Context.Core.Projects;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Features.GetProjects;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Projects;

[Trait("Lane", "unit")]
public sealed class ProjectFactsTests
{
    private const string Teams = """{"sources":{"linear":{"teams":["SPL"]}}}""";

    private static readonly DateTimeOffset _projectAt = RecordSamples.Now;
    private static readonly RecordKey _alpha = RecordKey.From("linear:project/alpha");
    private static readonly RecordKey _beta = RecordKey.From("linear:project/beta");
    private static readonly RecordKey _hidden = RecordKey.From("linear:project/hidden");

    private readonly FakeRecordStore _store = new();
    private readonly FakeSourceIdentityStore _identities = new();
    private readonly FakeIndexSync _sync = new();

    [Fact]
    public async Task OpenIssues_counts_open_visible_issues_of_the_project_and_no_others()
    {
        await Seed(
            Project(_alpha, "Alpha"), Project(_beta, "Beta"),
            Issue("linear:issue/1", _alpha), Issue("linear:issue/2", _alpha),
            Issue("linear:issue/3", _alpha, state: RecordState.Done),
            Issue("linear:issue/4", _alpha, audience: "linear:OTHER"),
            Issue("linear:issue/5", _beta));

        var answer = await Read([_alpha, _beta]);

        Assert.Equal([2, 1], answer.Projects.Select(p => p.OpenIssues));
    }

    [Fact]
    public async Task LastActivity_is_the_newest_change_of_the_project_or_one_of_its_issues()
    {
        var later = _projectAt.AddHours(5);
        await Seed(Project(_alpha, "Alpha"), Project(_beta, "Beta"), Issue("linear:issue/1", _alpha, updatedAt: later), Issue("linear:issue/2", _alpha, updatedAt: _projectAt.AddHours(-9)));

        var answer = await Read([_alpha, _beta]);

        Assert.Equal(later, answer.Projects[0].LastActivity);
        Assert.Equal(_projectAt, answer.Projects[1].LastActivity);
    }

    [Fact]
    public async Task Projects_keep_the_order_of_the_attached_keys()
    {
        await Seed(Project(_alpha, "Alpha"), Project(_beta, "Beta"));

        var answer = await Read([_beta, _alpha]);

        Assert.Equal([_beta, _alpha], answer.Projects.Select(p => p.Key));
    }

    [Fact]
    public async Task A_key_the_store_does_not_hold_is_left_out_not_an_error()
    {
        await Seed(Project(_alpha, "Alpha"));

        var answer = await Read([RecordKey.From("linear:project/unknown"), _alpha]);

        Assert.Equal([_alpha], answer.Projects.Select(p => p.Key));
    }

    [Fact]
    public async Task A_project_the_caller_cannot_see_is_left_out_of_projects_and_of_the_catalog()
    {
        await Seed(Project(_alpha, "Alpha"), Project(_hidden, "Hidden", audience: "linear:OTHER"));

        var answer = await Read([_hidden, _alpha]);

        Assert.Equal([_alpha], answer.Projects.Select(p => p.Key));
        Assert.Equal([_alpha], answer.Catalog.Select(p => p.Key));
    }

    [Fact]
    public async Task Catalog_holds_every_visible_stored_project_sorted_by_title()
    {
        await Seed(Project(_beta, "Beta"), Project(_alpha, "Alpha"), Project(RecordKey.From("linear:project/gamma"), "Gamma"));

        var answer = await Read([]);

        Assert.Equal(["Alpha", "Beta", "Gamma"], answer.Catalog.Select(p => p.Title), StringComparer.Ordinal);
        Assert.Empty(answer.Projects);
    }

    [Fact]
    public async Task Every_linear_project_has_its_url()
    {
        await Seed(Project(_alpha, "Alpha"), Project(_beta, "Beta"));

        var answer = await Read([_alpha]);

        Assert.All(answer.Catalog, p => Assert.Equal("https://linear.app/p/" + p.Key.Value["linear:project/".Length..], p.Url));
        Assert.Equal("https://linear.app/p/alpha", answer.Projects[0].Url);
    }

    [Fact]
    public async Task The_fields_the_page_shows_come_from_the_record()
    {
        await Seed(Project(_alpha, "Alpha") with
        {
            StateRaw = "started",
            Health = "atRisk",
            DueAt = new DateOnly(2026, 11, 14),
            ClosedAt = _projectAt.AddDays(1),
            Links = [new RecordLink("linear:user/u1", RecordLinkKind.LedBy, "Ada")],
        });

        var fact = (await Read([_alpha])).Projects.Single();

        Assert.Equal("Alpha", fact.Title);
        Assert.Equal("started", fact.StateRaw);
        Assert.Equal("atRisk", fact.Health);
        Assert.Equal(new DateTimeOffset(2026, 11, 14, 0, 0, 0, TimeSpan.Zero), fact.DueAt);
        Assert.Equal(_projectAt, fact.UpdatedAt);
        Assert.Equal(_projectAt.AddDays(1), fact.ClosedAt);
        Assert.Equal(new UserRef(ExternalId.From("u1"), "Ada"), fact.Lead);
        Assert.Null(fact.OpenPrs);
        Assert.Null(fact.Repos);
    }

    [Fact]
    public async Task The_owner_is_the_stored_identity_and_a_project_it_leads_says_so()
    {
        await Seed(Project(_alpha, "Alpha") with { Links = [new RecordLink("linear:user/me", RecordLinkKind.LedBy, "Me")] }, Project(_beta, "Beta") with { Links = [new RecordLink("linear:user/u2", RecordLinkKind.LedBy, "Bo")] }, Project(RecordKey.From("linear:project/c"), "C"));
        _ = await _identities.Write(new SourceIdentity(LinearSourceIds.Identity, "linear:user/me", "Me", RecordSamples.Now), TestContext.Current.CancellationToken);

        var answer = await Read([_alpha, _beta, RecordKey.From("linear:project/c")]);

        Assert.Equal(new UserRef(ExternalId.From("me"), "Me"), answer.Owner);
        Assert.Equal([true, false, false], answer.Projects.Select(p => p.LeadIsOwner));
    }

    [Fact]
    public async Task The_owner_is_null_when_the_source_has_not_said_who_it_is()
    {
        await Seed(Project(_alpha, "Alpha") with { Links = [new RecordLink("linear:user/me", RecordLinkKind.LedBy, "Me")] });

        var answer = await Read([_alpha]);

        Assert.Null(answer.Owner);
        Assert.False(answer.Projects[0].LeadIsOwner);
    }

    [Fact]
    public async Task Teams_are_the_profiles_linear_teams()
    {
        var answer = await Read([]);

        Assert.Equal(["SPL"], answer.Teams);
    }

    [Fact]
    public async Task FirstReadDone_follows_the_projects_feed_and_Counting_follows_the_issue_re_read()
    {
        await Seed(Project(_alpha, "Alpha"), Project(_beta, "Beta"));
        _sync.Standing = [Progress(LinearSourceIds.Projects, SyncPhase.Recent), Progress(LinearSourceIds.Issues, SyncPhase.Recent, reReading: 1200)];

        var reading = await Read([_alpha, _beta]);

        Assert.False(reading.FirstReadDone);
        Assert.All(reading.Projects, p => Assert.True(p.Counting));
        Assert.All(reading.Catalog, p => Assert.True(p.Counting));

        _sync.Standing = [Progress(LinearSourceIds.Projects, SyncPhase.Incremental), Progress(LinearSourceIds.Issues, SyncPhase.Incremental)];

        var done = await Read([_alpha, _beta]);

        Assert.True(done.FirstReadDone);
        Assert.All(done.Projects, p => Assert.False(p.Counting));
    }

    [Fact]
    public async Task Without_a_linear_feed_nothing_is_counting_and_the_first_read_is_not_done()
    {
        await Seed(Project(_alpha, "Alpha"));
        _sync.Standing = [];

        var answer = await Read([_alpha]);

        Assert.False(answer.FirstReadDone);
        Assert.False(answer.Projects[0].Counting);
    }

    [Fact]
    public async Task A_session_scoped_to_a_repo_outside_the_profile_sees_no_project()
    {
        await Seed(Project(_alpha, "Alpha"));
        using var section = System.Text.Json.JsonDocument.Parse(Teams);
        var scope = MapWorld.Session("not-a-profile-repo") with { Settings = section.RootElement.Clone() };

        var answer = (await Service().Handle(new GetProjectFactsRequest(new ProjectFactsRequest([_alpha]), scope), TestContext.Current.CancellationToken)).Value;

        Assert.Empty(answer.Projects);
        Assert.Empty(answer.Catalog);
    }

    private static SourceProgress Progress(SourceId source, SyncPhase phase, int? reReading = null) =>
        SourceProgress.From(source, phase, lastSync: null, lastCode: null, stored: reReading);

    private static StoredRecord Project(RecordKey key, string title, string audience = "linear:SPL") =>
        RecordSamples.Record(key.Value, source: LinearSourceIds.Projects) with
        {
            Kind = RecordKind.Project,
            Title = title,
            Url = "https://linear.app/p/" + key.Value["linear:project/".Length..],
            Audience = AudienceKey.From(audience),
            State = RecordState.Open,
            StateRaw = "started",
        };

    private static StoredRecord Issue(string key, RecordKey project, RecordState state = RecordState.Open, string audience = "linear:SPL", DateTimeOffset? updatedAt = null) =>
        RecordSamples.Record(key) with
        {
            State = state,
            Audience = AudienceKey.From(audience),
            UpdatedAt = updatedAt ?? RecordSamples.Now,
            Links = [new RecordLink(project.Value, RecordLinkKind.InProject, Title: null)],
        };

    private async Task Seed(params StoredRecord[] records)
    {
        var projects = records.Where(r => r.Kind == RecordKind.Project).Select(r => RecordSamples.Write(r, r.Title)).ToArray();
        var issues = records.Where(r => r.Kind == RecordKind.Issue).Select(r => RecordSamples.Write(r, r.Title)).ToArray();
        _ = await _store.Commit(RecordSamples.Batch(LinearSourceIds.Projects, 1, projects), TestContext.Current.CancellationToken);
        _ = await _store.Commit(RecordSamples.Batch(LinearSourceIds.Issues, 1, issues), TestContext.Current.CancellationToken);
    }

    private GetProjectFactsService Service() => new(_store, _identities, _sync);

    private async Task<ProjectFactsAnswer> Read(IReadOnlyList<RecordKey> attached)
    {
        var answer = await Service().Handle(new GetProjectFactsRequest(new ProjectFactsRequest(attached), MapWorld.HumanWith(Teams)), TestContext.Current.CancellationToken);
        Assert.False(answer.IsError);
        return answer.Value;
    }
}
