using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Context.Core;
using Aw.Context.Core.Onyx;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Fakes;

namespace Aw.Context.Tests.Onyx;

/// <summary>
/// The onyx source against the fake Onyx v4.9.0 (spec §5.1, §5.2, §10): the ContextSourceContract every source passes,
/// and what is the onyx source's own (keys, aliases, leaky hits, the key checks of its probe).
/// </summary>
[Trait("Lane", "integration")]
public sealed class OnyxSourceContractTests : ContextSourceContract<IContextSource>, IAsyncDisposable
{
    private const string Spl105 = "0e5a1b00-0000-4000-8000-000000000105";
    private const string Spl103 = "0e5a1b00-0000-4000-8000-000000000103";
    private readonly List<(FakeOnyxServer Server, OnyxSourceHarness Harness)> _created = [];

    [Fact]
    public void Describe_says_a_live_local_intent_source_that_cannot_cut_to_a_past_time_and_declares_no_limits()
    {
        using var harness = new OnyxSourceHarness(new Uri("http://127.0.0.1:3189"), "on_x");

        var descriptor = harness.Source.Describe();

        Assert.Equal(("onyx", SourceKind.Intent, SourceFreshness.Live, false, SourcePlacement.Local),
            (descriptor.Id.Value, descriptor.Kind, descriptor.Freshness, descriptor.SupportsAsOf, descriptor.Placement));
        Assert.Null(descriptor.Limits);   // today's local Onyx; a shared one would declare them (README X61)
    }

    [Fact]
    public async Task Search_returns_ranked_hits_with_uuid_keys_aliases_and_updated_at_versions()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);

        var hits = (await harness.Source.Search(OnyxSourceHarness.Query("minimum", "pane", "width"), TestContext.Current.CancellationToken)).Value;

        var top = hits[0];
        Assert.Equal(Enumerable.Range(1, hits.Count), hits.Select(h => h.Rank));
        Assert.Equal("onyx", top.Ref.Source.Value);
        Assert.Equal(Spl105, top.Ref.Id.Value);
        Assert.Equal("linear:issue/" + Spl105, top.Ref.Canonical.Value);
        Assert.Equal(["SPL-105"], top.Aliases);
        Assert.Equal("2026-08-12T15:00:00.0000000Z", top.Ref.Version.Value);
        Assert.Equal(new Uri("https://linear.app/fixture-labs/issue/SPL-105"), top.Ref.Permalink);
        Assert.Equal("[SPL-105] Resize panes with the keyboard", top.Title);
        Assert.Equal(OnyxSourceHarness.Now, top.AsOf);
        Assert.All(hits, h => Assert.False(h.Leaky));
        var request = Assert.Single(onyx.Requests);
        Assert.Equal(("POST", "/api/admin/search", 200), (request.Method, request.Path, request.Status));
    }

    [Fact]
    public async Task Search_keys_a_slack_thread_on_its_channel_and_ts_and_applies_the_limit_client_side()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);

        var all = (await harness.Source.Search(OnyxSourceHarness.Query("resize step"), TestContext.Current.CancellationToken)).Value;
        var one = (await harness.Source.Search(OnyxSourceHarness.Query("resize step") with { Limit = 1 }, TestContext.Current.CancellationToken)).Value;

        Assert.Contains(all, h => h.Ref.Canonical.Value == "slack:C0FIXTURE01/1784972400.000100" && h.Aliases.Count == 0);
        Assert.True(all.Count > 1);
        Assert.Equal(all[0].Ref, Assert.Single(one).Ref);
        Assert.Equal(2, onyx.Requests.Count);
        var note = Assert.Single(((ISourceNotes)harness.Source).TakeNotes());   // the first search dropped nothing; the second names the cut
        Assert.Contains("past the limit of 1", note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_leaves_out_documents_onyx_marks_hidden()
    {
        // Admin search includes documents an Onyx admin hid (SearchDoc.hidden); a standard search never returns them.
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions { OnyxPlant = static json => Hide(json, Spl105) });
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);

        var hits = (await harness.Source.Search(OnyxSourceHarness.Query("minimum", "pane", "width"), TestContext.Current.CancellationToken)).Value;

        Assert.NotEmpty(hits);
        Assert.DoesNotContain(hits, h => h.Ref.Id.Value == Spl105);
        Assert.Equal(Enumerable.Range(1, hits.Count), hits.Select(h => h.Rank));
        var note = Assert.Single(((ISourceNotes)harness.Source).TakeNotes());
        Assert.Contains("1 hidden by an Onyx admin", note, StringComparison.Ordinal);
        Assert.Empty(((ISourceNotes)harness.Source).TakeNotes());   // read once
    }

    [Fact]
    public async Task Search_keeps_hits_changed_after_as_of_and_marks_them_leaky()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        var asOf = new DateTimeOffset(2026, 7, 29, 9, 30, 0, TimeSpan.Zero);

        var hits = (await harness.Source.Search(OnyxSourceHarness.Query("pane") with { AsOf = asOf }, TestContext.Current.CancellationToken)).Value;

        Assert.True(Assert.Single(hits, h => h.Ref.Id.Value == Spl105).Leaky);
        Assert.False(Assert.Single(hits, h => h.Ref.Id.Value == Spl103).Leaky);
    }

    [Fact]
    public async Task Fetch_joins_the_documents_chunks_in_order_with_the_hits_title_author_and_updated_at()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        var hit = (await harness.Source.Search(OnyxSourceHarness.Query("minimum pane width"), TestContext.Current.CancellationToken)).Value[0];

        var record = (await harness.Source.Fetch(hit.Ref, TestContext.Current.CancellationToken)).Value;

        Assert.Equal(hit.Ref, record.Ref);
        Assert.Equal(RecordKind.Issue, record.Kind);
        Assert.Equal(hit.Title, record.Title);
        Assert.Equal("Avery Fixture", record.Author);
        Assert.Equal(new DateTimeOffset(2026, 8, 12, 15, 0, 0, TimeSpan.Zero), record.UpdatedAt);
        Assert.StartsWith("resize-pane-left and resize-pane-right", record.Text, StringComparison.Ordinal);
        Assert.Contains("border by the paneResizeStep setting.\n\nDecision: the minimum pane width is 10 columns", record.Text, StringComparison.Ordinal);
        Assert.EndsWith("Ten columns truncated every prompt in user testing.", record.Text, StringComparison.Ordinal);
        Assert.Equal(
            ["/api/admin/search", $"/api/document/document-size-info?document_id={Spl105}",
             .. Enumerable.Range(0, 4).Select(i => string.Create(CultureInfo.InvariantCulture, $"/api/document/chunk-info?document_id={Spl105}&chunk_id={i}"))],
            onyx.Requests.Select(r => r.Path), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Fetch_without_a_search_takes_the_kind_from_the_key_and_updated_at_from_the_version()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        var reference = new SourceRef(SourceId.From("onyx"), ExternalId.From("C0FIXTURE01__1784972400.000100"),
            SourceVersion.From("2026-07-25T09:40:00.0000000Z"), Permalink: null, RecordKey.From("slack:C0FIXTURE01/1784972400.000100"));

        var record = (await harness.Source.Fetch(reference, TestContext.Current.CancellationToken)).Value;

        Assert.Equal(RecordKind.Message, record.Kind);
        Assert.Equal("C0FIXTURE01__1784972400.000100", record.Title);
        Assert.Null(record.Author);
        Assert.Equal(new DateTimeOffset(2026, 7, 25, 9, 40, 0, TimeSpan.Zero), record.UpdatedAt);
        Assert.Contains("paneResizeStep", record.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fetch_of_a_document_onyx_does_not_have_is_not_found_with_052()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        var reference = new SourceRef(SourceId.From("onyx"), ExternalId.From("gone"), SourceVersion.From("2026-07-25T09:40:00.0000000Z"), Permalink: null, RecordKey.From("onyx:web/gone"));

        var result = await harness.Source.Fetch(reference, TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.OnyxDocumentNotFound.Code, result.Error.Code);
        Assert.Equal(Kernel.ErrorKind.NotFound, result.Error.Kind);
    }

    [Fact]
    public async Task Versions_is_unavailable_and_asks_onyx_nothing()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        var reference = new SourceRef(SourceId.From("onyx"), ExternalId.From(Spl105), SourceVersion.From("2026-08-12T15:00:00.0000000Z"), Permalink: null, RecordKey.From("linear:issue/" + Spl105));

        var result = await harness.Source.Versions([reference], TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.SourceUnavailable.Code, result.Error.Code);
        Assert.Empty(onyx.Requests);
    }

    [Fact]
    public async Task Probe_is_ok_for_a_service_account_admin_key_and_says_so()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);

        var health = await harness.Source.Probe(TestContext.Current.CancellationToken);

        Assert.Equal(SourceState.Ok, health.State);
        Assert.Null(health.Code);
        Assert.Contains("v4.9.0", health.Detail, StringComparison.Ordinal);
        Assert.Contains("admin, service account", health.Detail, StringComparison.Ordinal);
        Assert.Equal(["/api/version", "/api/me"], onyx.Requests.Select(r => r.Path), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Probe_is_ok_for_the_dedicated_sindri_users_admin_token_and_says_so()
    {
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions { OnyxAccountType = "STANDARD", OnyxAccountEmail = OnyxSettings.DedicatedAccountEmail });
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);

        var health = await harness.Source.Probe(TestContext.Current.CancellationToken);

        Assert.Equal(SourceState.Ok, health.State);
        Assert.Null(health.Code);
        Assert.Contains("admin, Sindri's dedicated user", health.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("someone@example.com")]
    [InlineData(null)]
    public async Task Probe_warns_when_the_admin_token_is_another_persons_standard_account(string? email)
    {
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions { OnyxAccountType = "STANDARD", OnyxAccountEmail = email });
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);

        var health = await harness.Source.Probe(TestContext.Current.CancellationToken);

        Assert.Equal(SourceState.Degraded, health.State);
        Assert.Equal(ContextErrors.OnyxKeyNotServiceAccount.Code, health.Code);
        Assert.Contains("STANDARD", health.Detail, StringComparison.Ordinal);
        Assert.Contains(OnyxSettings.DedicatedAccountEmail, health.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("someone@example.com", health.Detail, StringComparison.Ordinal);   // a person's address is not echoed
    }

    [Fact]
    public async Task Probe_matches_the_dedicated_email_without_regard_to_case()
    {
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions { OnyxAccountType = "STANDARD", OnyxAccountEmail = "Sindri@Example.COM" });
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);

        var health = await harness.Source.Probe(TestContext.Current.CancellationToken);

        Assert.Equal(SourceState.Ok, health.State);
    }

    [Fact]
    public async Task Probe_of_a_key_without_admin_is_unavailable_with_050_and_search_is_refused_alike()
    {
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions { OnyxAdmin = false });
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);

        var health = await harness.Source.Probe(TestContext.Current.CancellationToken);
        var search = await harness.Source.Search(OnyxSourceHarness.Query("pane"), TestContext.Current.CancellationToken);

        Assert.Equal((SourceState.Unavailable, ContextErrors.OnyxKeyRefused.Code), (health.State, health.Code));
        Assert.Contains("admin", health.Detail, StringComparison.Ordinal);
        Assert.Equal(ContextErrors.OnyxKeyRefused.Code, search.Error.Code);
    }

    [Fact]
    public async Task Probe_of_another_onyx_version_is_unavailable_with_055()
    {
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions { OnyxVersion = "v4.8.1" });
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);

        var health = await harness.Source.Probe(TestContext.Current.CancellationToken);

        Assert.Equal((SourceState.Unavailable, ContextErrors.OnyxNotPinnedOrLoopback.Code), (health.State, health.Code));
        Assert.Equal(["/api/version"], onyx.Requests.Select(r => r.Path), StringComparer.Ordinal);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var (server, harness) in _created)
        {
            harness.Dispose();
            await server.DisposeAsync();
        }
    }

    private static string Hide(string json, string documentId)
    {
        if (JsonNode.Parse(json) is not JsonObject { } reply || reply["documents"] is not JsonArray documents)
        {
            return json;
        }

        foreach (var document in documents.OfType<JsonObject>().Where(d => (string?)d["document_id"] == documentId))
        {
            document["hidden"] = true;
        }

        return reply.ToJsonString();
    }

    protected override bool ChecksVersions => false;

    protected override async Task<IContextSource> Create()
    {
        var onyx = await FakeOnyxServer.Start();
        var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        _created.Add((onyx, harness));
        return harness.Source;
    }
}
