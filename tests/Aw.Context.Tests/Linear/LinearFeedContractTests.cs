using System.Text.Json.Nodes;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sync;
using Aw.Context.Infrastructure.Linear;
using Aw.Context.Testing.Records;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Linear;

/// <summary>The Linear record feed over a fake Linear: it honours the RecordFeed contract (spec 2026-10-27 sindri-owned index §5.2, U9).</summary>
[Trait("Lane", "unit")]
public sealed class LinearFeedContractTests : RecordFeedContract<RecordFeed>
{
    // why: the contract hands the feed arbitrary items (any keys and times), which FakeLinearServer's fixed recordings can't be seeded with; the query itself is run against the fake server in LinearFeedFakeServerTests
    protected override RecordFeed Create(IReadOnlyList<RecordItem> items) =>
        FeedOf([.. items.Select(i => FakeLinearIssues.Node(i.Key.Value["linear:issue/".Length..], i.UpdatedAt!.Value))]).Feed;

    internal static (RecordFeed Feed, FakeLinearIssues Fake) FeedOf(IReadOnlyList<JsonObject> nodes)
    {
        var fake = new FakeLinearIssues(nodes);
        var settings = LinearSettings.Create("http://127.0.0.1:1/graphql", "env:LINEAR_FIXTURE_KEY", ["SPL"]).Value;
        var parts = new LinearSourceParts(new HttpClient(fake), static _ => "fixture-key", new FakeTimeProvider(new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero)), new CapturingLogger<LinearClient>());
        return (new LinearSources(parts).CreateFeed(settings), fake);
    }
}
