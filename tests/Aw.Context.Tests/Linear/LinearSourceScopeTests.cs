using System.Diagnostics;
using Aw.Context.Core;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Linear;
using Aw.Context.Testing.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Linear;

/// <summary>
/// The linear source over a fake Linear server that holds more than the adapter's page (spec §5.2, D18): the profile's teams are applied by Linear, not by a list the
/// adapter keeps; lists are paged; the deadline is shared by a search's calls; a rate limit and a long thread say so; a project carries its latest updates; Versions applies the scope.
/// </summary>
[Trait("Lane", "integration")]
public sealed class LinearSourceScopeTests : IAsyncDisposable
{
    private const string Prefix = "0e5a1b00-0000-4000-8000-";
    private const string Doc = Prefix + "000000000020";
    private const string Project = Prefix + "000000000010";
    private const string Update = Prefix + "000000000031";
    private const string Spl105 = Prefix + "000000000105";
    private readonly List<(FakeLinearServer Server, LinearSourceHarness Harness)> _created = [];

    [Fact]
    public async Task A_profile_project_is_found_and_fetched_among_more_than_250_projects_of_another_team()
    {
        var (_, harness) = await Start(new FakeServerOptions { LinearExtraProjects = 300, LinearNewestFirst = true });

        var hits = (await harness.Source.Search(LinearSourceHarness.Query("panes"), TestContext.Current.CancellationToken)).Value;
        var doc = hits.Single(h => h.Ref.Canonical.Value == "linear:doc/" + Doc);
        var project = hits.Single(h => h.Ref.Canonical.Value == "linear:project/" + Project);
        var fetchedDoc = await harness.Source.Fetch(doc.Ref, TestContext.Current.CancellationToken);
        var fetchedProject = await harness.Source.Fetch(project.Ref, TestContext.Current.CancellationToken);
        var fetchedUpdate = await harness.Source.Fetch(Ref(project.Ref, "update", Update, "2026-07-17T16:00:00.0000000Z"), TestContext.Current.CancellationToken);

        Assert.True(fetchedDoc.IsSuccess, fetchedDoc.IsError ? fetchedDoc.Error.Message : "");
        Assert.True(fetchedProject.IsSuccess);
        Assert.True(fetchedUpdate.IsSuccess, fetchedUpdate.IsError ? fetchedUpdate.Error.Message : "");
    }

    [Fact]
    public async Task A_search_for_more_hits_than_one_page_pages_until_the_limit()
    {
        var (linear, harness) = await Start(new FakeServerOptions { LinearExtraProjects = 300, LinearExtraProjectsTeam = "SPL", LinearNewestFirst = true });

        var hits = (await harness.Source.Search(new SourceQuery(["Filler"], AsOf: null, Limit: 300, LinearSourceHarness.Patient), TestContext.Current.CancellationToken)).Value;

        Assert.Equal(300, hits.Count);
        Assert.Equal(300, hits.Select(h => h.Ref.Canonical.Value).Distinct(StringComparer.Ordinal).Count());
        Assert.True(linear.Requests.Where(r => r.Operation.Contains("projects", StringComparison.Ordinal)).Skip(1).Any());
    }

    [Fact]
    public async Task A_search_spends_one_deadline_across_its_calls()
    {
        var (linear, harness) = await Start(new FakeServerOptions { Delay = TimeSpan.FromMilliseconds(300) });
        var query = new SourceQuery(["pane", "SPL-101", "SPL-102", "SPL-103"], AsOf: null, Limit: 20, TimeSpan.FromMilliseconds(700));
        var timer = Stopwatch.StartNew();

        var result = await harness.Source.Search(query, TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.SourceUnavailable.Code, result.Error.Code);
        Assert.True(timer.Elapsed < TimeSpan.FromMilliseconds(1500), $"took {timer.Elapsed}");
        Assert.True(linear.Requests.Count <= 3);
    }

    [Fact]
    public async Task A_project_record_lists_its_latest_updates()
    {
        var (_, harness) = await Start();
        var project = (await harness.Source.Search(LinearSourceHarness.Query("Split panes"), TestContext.Current.CancellationToken)).Value.Single(h => h.Ref.Canonical.Value.StartsWith("linear:project/", StringComparison.Ordinal));

        var record = (await harness.Source.Fetch(project.Ref, TestContext.Current.CancellationToken)).Value;

        Assert.Contains("\n\nUpdates:\n", record.Text, StringComparison.Ordinal);
        Assert.Contains("atRisk", record.Text, StringComparison.Ordinal);
        Assert.Contains("The minimum pane width decision changed to 20 columns", record.Text, StringComparison.Ordinal);
        Assert.True(record.Text.IndexOf("atRisk", StringComparison.Ordinal) < record.Text.IndexOf("onTrack", StringComparison.Ordinal));   // newest first
    }

    [Theory]
    [InlineData(LinearRateLimit.Http429)]
    [InlineData(LinearRateLimit.GraphQl)]
    public async Task A_rate_limited_linear_is_unavailable_and_says_to_retry_later(LinearRateLimit mode)
    {
        var (_, harness) = await Start(new FakeServerOptions { LinearRateLimit = mode });

        var search = await harness.Source.Search(LinearSourceHarness.Query("pane"), TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.SourceUnavailable.Code, search.Error.Code);
        Assert.Contains("rate limited, retry later", search.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_issue_with_more_than_fifty_comments_says_the_rest_are_not_shown()
    {
        var (_, harness) = await Start(new FakeServerOptions { LinearExtraComments = 60 });
        var hit = (await harness.Source.Search(LinearSourceHarness.Query("keyboard"), TestContext.Current.CancellationToken)).Value.Single();

        var record = (await harness.Source.Fetch(hit.Ref, TestContext.Current.CancellationToken)).Value;

        Assert.Contains("(more comments not shown)", record.Text, StringComparison.Ordinal);
        Assert.Equal(Spl105, hit.Ref.Id.Value);
    }

    [Fact]
    public async Task An_issue_with_few_comments_has_no_marker()
    {
        var (_, harness) = await Start();
        var hit = (await harness.Source.Search(LinearSourceHarness.Query("keyboard"), TestContext.Current.CancellationToken)).Value.Single();

        var record = (await harness.Source.Fetch(hit.Ref, TestContext.Current.CancellationToken)).Value;

        Assert.DoesNotContain("not shown", record.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Versions_answers_null_for_records_outside_the_profiles_teams_as_fetch_does()
    {
        var linear = await FakeLinearServer.Start();
        using var inside = new LinearSourceHarness(linear.Url, linear.Token, "SPL");
        using var outside = new LinearSourceHarness(linear.Url, linear.Token, "ENG");
        _created.Add((linear, inside));
        var issue = (await inside.Source.Search(LinearSourceHarness.Query("keyboard"), TestContext.Current.CancellationToken)).Value.Single();
        SourceRef[] refs = [issue.Ref, Ref(issue.Ref, "doc", Doc, "x"), Ref(issue.Ref, "project", Project, "x"), Ref(issue.Ref, "update", Update, "x")];

        var seen = (await inside.Source.Versions(refs, TestContext.Current.CancellationToken)).Value;
        var hidden = (await outside.Source.Versions(refs, TestContext.Current.CancellationToken)).Value;

        Assert.All(seen, v => Assert.NotNull(v));
        Assert.All(hidden, v => Assert.Null(v));
    }

    [Fact]
    public async Task A_key_that_did_not_resolve_is_asked_for_again_on_the_next_call()
    {
        var linear = await FakeLinearServer.Start();
        using var http = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false });
        var asked = 0;
        var settings = LinearSettings.Create(linear.Url.ToString(), LinearSourceHarness.Pointer, ["SPL"]).Value;
        var source = new LinearSources(new LinearSourceParts(http, _ => ++asked == 1 ? null : linear.Token, new FakeTimeProvider(LinearSourceHarness.Now), new CapturingLogger<LinearClient>())).Create(settings);

        var first = await source.Search(LinearSourceHarness.Query("pane"), TestContext.Current.CancellationToken);
        var second = await source.Search(LinearSourceHarness.Query("pane"), TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.SourceNotConfigured.Code, first.Error.Code);
        Assert.True(second.IsSuccess);
        await linear.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var (server, harness) in _created)
        {
            harness.Dispose();
            await server.DisposeAsync();
        }
    }

    private static SourceRef Ref(SourceRef like, string kind, string id, string version) =>
        like with { Id = ExternalId.From(id), Version = SourceVersion.From(version), Canonical = RecordKey.From($"linear:{kind}/{id}") };

    private async Task<(FakeLinearServer Linear, LinearSourceHarness Harness)> Start(FakeServerOptions? options = null)
    {
        var linear = await FakeLinearServer.Start(options);
        var harness = new LinearSourceHarness(linear.Url, linear.Token);
        _created.Add((linear, harness));
        return (linear, harness);
    }
}
