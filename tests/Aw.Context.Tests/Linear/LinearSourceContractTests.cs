using Aw.Context.Core;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Fakes;
using Aw.Kernel;

namespace Aw.Context.Tests.Linear;

/// <summary>
/// The linear source against the fake Linear GraphQL API (spec §5.1, §5.2, §10, D18): the ContextSourceContract every source passes, and what is the
/// linear source's own (keys, aliases, the team and updatedAt filters, the records' text, versions, the key checks of its probe).
/// </summary>
[Trait("Lane", "integration")]
public sealed class LinearSourceContractTests : ContextSourceContract<IContextSource>, IAsyncDisposable
{
    private const string Prefix = "0e5a1b00-0000-4000-8000-";
    private const string Spl105 = Prefix + "000000000105";
    private const string Spl103 = Prefix + "000000000103";
    private const string Project = Prefix + "000000000010";
    private const string Doc = Prefix + "000000000020";
    private const string Update = Prefix + "000000000031";
    private readonly List<(FakeLinearServer Server, LinearSourceHarness Harness)> _created = [];

    [Fact]
    public void Describe_says_a_live_local_intent_source_that_cannot_cut_to_a_past_time_and_declares_no_limits()
    {
        using var harness = new LinearSourceHarness(new Uri("http://127.0.0.1:4010/graphql"), "k");

        var descriptor = harness.Source.Describe();

        Assert.Equal(("linear", SourceKind.Intent, SourceFreshness.Live, false, SourcePlacement.Local),
            (descriptor.Id.Value, descriptor.Kind, descriptor.Freshness, descriptor.SupportsAsOf, descriptor.Placement));
        Assert.Null(descriptor.Limits);
    }

    [Fact]
    public async Task Search_by_title_finds_the_document_the_project_and_the_issues_with_their_canonical_keys_in_that_order()
    {
        var (linear, harness) = await Start();

        var hits = (await harness.Source.Search(LinearSourceHarness.Query("panes"), TestContext.Current.CancellationToken)).Value;

        var keys = hits.Select(h => h.Ref.Canonical.Value).ToList();
        Assert.Equal(Enumerable.Range(1, hits.Count), hits.Select(h => h.Rank));
        Assert.True(keys.IndexOf("linear:doc/" + Doc) < keys.IndexOf("linear:project/" + Project));
        Assert.True(keys.IndexOf("linear:project/" + Project) < keys.IndexOf("linear:issue/" + Spl105));
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(hits, h => Assert.Equal(("linear", false, LinearSourceHarness.Now), (h.Ref.Source.Value, h.Leaky, h.AsOf)));
        Assert.All(linear.Requests, r => Assert.Equal(("POST", "/graphql", 200), (r.Method, r.Path, r.Status)));
    }

    [Fact]
    public async Task An_issue_hit_has_the_canonical_key_the_identifier_as_an_alias_and_its_updated_at_as_the_version()
    {
        var (_, harness) = await Start();

        var issue = (await harness.Source.Search(LinearSourceHarness.Query("keyboard"), TestContext.Current.CancellationToken)).Value.Single();

        Assert.Equal(Spl105, issue.Ref.Id.Value);
        Assert.Equal("linear:issue/" + Spl105, issue.Ref.Canonical.Value);
        Assert.Equal(["SPL-105"], issue.Aliases);
        Assert.Equal("2026-08-12T15:00:00.0000000Z", issue.Ref.Version.Value);
        Assert.Equal(new Uri("https://linear.app/fixture-labs/issue/SPL-105"), issue.Ref.Permalink);
        Assert.Equal("[SPL-105] Resize panes with the keyboard", issue.Title);
    }

    [Fact]
    public async Task An_issue_identifier_is_looked_up_by_key_and_comes_first_and_one_outside_the_profiles_teams_is_not_asked_for()
    {
        var (linear, harness) = await Start();

        var hits = (await harness.Source.Search(LinearSourceHarness.Query("pane", "SPL-103"), TestContext.Current.CancellationToken)).Value;
        var before = linear.Requests.Count;
        var other = (await harness.Source.Search(LinearSourceHarness.Query("ENG-1"), TestContext.Current.CancellationToken)).Value;

        Assert.Equal("linear:issue/" + Spl103, hits[0].Ref.Canonical.Value);
        Assert.Equal(["SPL-103"], hits[0].Aliases);
        Assert.Equal(1, hits.Count(h => h.Ref.Canonical.Value == "linear:issue/" + Spl103));
        Assert.Empty(other);
        Assert.DoesNotContain(linear.Requests.Skip(before), r => string.Equals(r.Operation, "issue", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_updated_since_filter_is_applied_by_the_source_before_its_cap()
    {
        var (_, harness) = await Start();

        var hits = (await harness.Source.Search(LinearSourceHarness.Query("panes") with { UpdatedSince = new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.Zero) }, TestContext.Current.CancellationToken)).Value;

        Assert.Equal(["linear:project/" + Project], hits.Select(h => h.Ref.Canonical.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Only_the_profiles_teams_are_read_so_another_teams_profile_finds_nothing()
    {
        var linear = await FakeLinearServer.Start();
        using var harness = new LinearSourceHarness(linear.Url, linear.Token, "ENG");
        _created.Add((linear, harness));

        var hits = (await harness.Source.Search(LinearSourceHarness.Query("panes"), TestContext.Current.CancellationToken)).Value;

        Assert.Empty(hits);
    }

    [Fact]
    public async Task Search_keeps_hits_changed_after_as_of_and_marks_them_leaky()
    {
        var (_, harness) = await Start();
        var asOf = new DateTimeOffset(2026, 7, 29, 9, 30, 0, TimeSpan.Zero);

        var hits = (await harness.Source.Search(LinearSourceHarness.Query("pane") with { AsOf = asOf }, TestContext.Current.CancellationToken)).Value;

        Assert.True(Assert.Single(hits, h => h.Ref.Id.Value == Spl105).Leaky);
        Assert.False(Assert.Single(hits, h => h.Ref.Id.Value == Prefix + "000000000102").Leaky);
    }

    [Fact]
    public async Task Fetching_an_issue_gives_its_description_its_comments_and_the_pull_requests_attached_to_it()
    {
        var (_, harness) = await Start();
        var hit = (await harness.Source.Search(LinearSourceHarness.Query("keyboard"), TestContext.Current.CancellationToken)).Value.Single();

        var record = (await harness.Source.Fetch(hit.Ref, TestContext.Current.CancellationToken)).Value;

        Assert.Equal((RecordKind.Issue, "[SPL-105] Resize panes with the keyboard", "Avery Fixture"), (record.Kind, record.Title, record.Author));
        Assert.Equal(new DateTimeOffset(2026, 8, 12, 15, 0, 0, TimeSpan.Zero), record.UpdatedAt);
        Assert.StartsWith("resize-pane-left and resize-pane-right", record.Text, StringComparison.Ordinal);
        Assert.Contains("\n\nComments:\n", record.Text, StringComparison.Ordinal);
        Assert.Contains("Decision: the minimum pane width is 10 columns", record.Text, StringComparison.Ordinal);
        Assert.Contains("\n\nAttachments:\n- ", record.Text, StringComparison.Ordinal);
        Assert.EndsWith("/pull/5", record.Text.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fetching_a_document_a_project_and_a_project_update_gives_their_text_and_kinds()
    {
        var (_, harness) = await Start();
        var doc = (await harness.Source.Search(LinearSourceHarness.Query("PRD"), TestContext.Current.CancellationToken)).Value.Single();
        var project = (await harness.Source.Search(LinearSourceHarness.Query("Split panes"), TestContext.Current.CancellationToken)).Value.Single(h => h.Ref.Canonical.Value.StartsWith("linear:project/", StringComparison.Ordinal));
        var update = Ref(project.Ref, "update", Update, "2026-07-17T16:00:00.0000000Z");

        var document = (await harness.Source.Fetch(doc.Ref, TestContext.Current.CancellationToken)).Value;
        var proj = (await harness.Source.Fetch(project.Ref, TestContext.Current.CancellationToken)).Value;
        var note = (await harness.Source.Fetch(update, TestContext.Current.CancellationToken)).Value;

        Assert.Equal((RecordKind.Doc, "PRD: Split panes"), (document.Kind, document.Title));
        Assert.StartsWith("# PRD: Split panes", document.Text, StringComparison.Ordinal);
        Assert.Equal((RecordKind.Project, "Split panes"), (proj.Kind, proj.Title));
        Assert.Contains("Panes inside a tab", proj.Text, StringComparison.Ordinal);
        Assert.Equal(RecordKind.Project, note.Kind);
        Assert.Contains("Tab layout restore (SPL-104) is next.", note.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fetching_a_record_that_is_gone_or_changed_since_it_was_cited_is_not_found_with_073()
    {
        var (_, harness) = await Start();
        var hit = (await harness.Source.Search(LinearSourceHarness.Query("keyboard"), TestContext.Current.CancellationToken)).Value.Single();

        var gone = await harness.Source.Fetch(Ref(hit.Ref, "issue", Prefix + "00000000ffff", hit.Ref.Version.Value), TestContext.Current.CancellationToken);
        var changed = await harness.Source.Fetch(hit.Ref with { Version = SourceVersion.From("2026-01-01T00:00:00.0000000Z") }, TestContext.Current.CancellationToken);

        Assert.Equal((ContextErrors.LinearRecordNotFound.Code, ErrorKind.NotFound), (gone.Error.Code, gone.Error.Kind));
        Assert.Equal(ContextErrors.LinearRecordNotFound.Code, changed.Error.Code);
    }

    [Fact]
    public async Task Versions_asks_once_per_kind_and_once_more_for_the_projects_of_documents_and_updates_and_answers_null_for_a_record_that_is_gone()
    {
        var (linear, harness) = await Start();
        var issue = (await harness.Source.Search(LinearSourceHarness.Query("keyboard"), TestContext.Current.CancellationToken)).Value.Single();
        var refs = new[]
        {
            issue.Ref,
            Ref(issue.Ref, "doc", Doc, "x"),
            Ref(issue.Ref, "project", Project, "x"),
            Ref(issue.Ref, "issue", Prefix + "00000000ffff", "x"),
            Ref(issue.Ref, "update", Update, "x"),
        };
        var before = linear.Requests.Count;

        var versions = (await harness.Source.Versions(refs, TestContext.Current.CancellationToken)).Value;

        Assert.Equal(["2026-08-12T15:00:00.0000000Z", "2026-07-20T12:00:00.0000000Z", "2026-08-14T16:00:00.0000000Z", null, "2026-07-17T16:00:00.0000000Z"], versions.Select(v => v?.Value), StringComparer.Ordinal);
        Assert.Equal(6, linear.Requests.Count - before);   // four kinds, and a project read each for the documents and the updates
    }

    [Fact]
    public async Task Probe_is_ok_and_names_the_teams_it_read_and_a_refused_key_is_unavailable_with_071_for_search_alike()
    {
        var (_, harness) = await Start();
        var linear = await FakeLinearServer.Start();
        using var refused = new LinearSourceHarness(linear.Url, "not-the-key");
        _created.Add((linear, refused));

        var ok = await harness.Source.Probe(TestContext.Current.CancellationToken);
        var health = await refused.Source.Probe(TestContext.Current.CancellationToken);
        var search = await refused.Source.Search(LinearSourceHarness.Query("pane"), TestContext.Current.CancellationToken);

        Assert.Equal((SourceState.Ok, null), (ok.State, ok.Code));
        Assert.Contains("SPL", ok.Detail, StringComparison.Ordinal);
        Assert.Equal((SourceState.Unavailable, ContextErrors.LinearKeyRefused.Code), (health.State, health.Code));
        Assert.Equal(ContextErrors.LinearKeyRefused.Code, search.Error.Code);
    }

    [Fact]
    public async Task A_pointer_that_resolves_to_nothing_is_002_and_the_key_never_reaches_the_log()
    {
        var linear = await FakeLinearServer.Start();
        using var harness = new LinearSourceHarness(linear.Url, token: null);
        _created.Add((linear, harness));

        var search = await harness.Source.Search(LinearSourceHarness.Query("pane"), TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.SourceNotConfigured.Code, search.Error.Code);
        Assert.Empty(linear.Requests);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var (server, harness) in _created)
        {
            harness.Dispose();
            await server.DisposeAsync();
        }
    }

    protected override async Task<IContextSource> Create() => (await Start()).Harness.Source;

    private static SourceRef Ref(SourceRef like, string kind, string id, string version) =>
        like with { Id = ExternalId.From(id), Version = SourceVersion.From(version), Canonical = RecordKey.From($"linear:{kind}/{id}") };

    private async Task<(FakeLinearServer Linear, LinearSourceHarness Harness)> Start()
    {
        var linear = await FakeLinearServer.Start();
        var harness = new LinearSourceHarness(linear.Url, linear.Token);
        _created.Add((linear, harness));
        return (linear, harness);
    }
}
