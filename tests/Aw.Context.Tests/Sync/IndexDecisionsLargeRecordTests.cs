using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Sync;

/// <summary>One huge stored record must not fail the index-decisions run for the rest of its feed (#1769): the linker's regexes time out on it.</summary>
[Trait("Lane", "integration")]
public sealed class IndexDecisionsLargeRecordTests
{
    private const string Quote = "the minimum pane width is 20 columns";
    private const string Resize = "feature:layout.pane-resize";
    private static readonly RecordKey _small = RecordKey.From("github:issue/acme/shop/7");
    private static readonly RecordKey _big = RecordKey.From("github:issue/acme/shop/8");
    private static readonly string _hugePath = string.Concat(Enumerable.Repeat("é/", 24_000_000));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RecordItem Issue(RecordKey key, string body, DateTimeOffset now) => new(
        key, RecordKind.Issue, "Pane resize limits", body, Author: null, Url: null, RecordState.Open, StateRaw: null,
        CreatedAt: null, now.AddDays(-1), ClosedAt: null, SourceVersion.From("v1"), Links: [], Repo: null, Audience: AudienceKey.From("repo:acme/shop"));

    [Fact]
    public async Task A_record_too_large_to_link_in_time_does_not_fail_the_run()
    {
        var now = DateTimeOffset.Parse("2026-08-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);   // MapWorld's clock
        using var world = new MapWorld(feeds: new FakeRecordFeedCatalog(new FakeRecordFeed(RecordSamples.Github,
            [Issue(_small, $"Pane resize: we agreed {Quote}, anything narrower is clamped.", now), Issue(_big, _hugePath, now)])));
        _ = await world.ImportFixture();
        world.Model.Answer = $$"""{"decisions":[{"quote":"{{Quote}}","wording":"Panes are at least 20 columns wide","decides":["{{Resize}}"]}]}""";
        var scope = MapWorld.HumanWith("{\"labeling\":{\"enabled\":true}}");
        _ = await world.Engine.Tick(scope, Ct);

        var decided = await world.Engine.Decide(scope, Ct);

        Assert.True(decided.IsSuccess, decided.IsError ? decided.Error.Message : null);
        Assert.Null(Assert.Single(decided.Value.Sources).Code);
        var layer = (await world.Intents.Layer(Ct)).Value;
        Assert.Equal(_small, Assert.Single(layer.Decisions).Record);
        Assert.Contains(layer.Records, r => r.Key == _big);
    }
}
