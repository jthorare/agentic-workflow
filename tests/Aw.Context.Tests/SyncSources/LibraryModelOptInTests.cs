using Aw.Context.Core.Engine;
using Aw.Context.Core.Library;
using Aw.Context.Core.Models;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Features.SyncSources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.SyncSources;

/// <summary>A library record reaches no model unless its root opted in (PR review page spec 2026-11-04 D13): here the decision pass; the rerank and the concept synthesis have their own tests.</summary>
[Trait("Lane", "unit")]
public sealed class LibraryModelOptInTests
{
    private const string Quote = "the minimum pane width is 20 columns";
    private const string Resize = "feature:layout.pane-resize";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<int> Extractions(IReadOnlyList<string> modelRoots, string audience = "repo:acme/shop")
    {
        using var world = new MapWorld();
        _ = await world.ImportFixture();
        world.Model.Answer = $$"""{"decisions":[{"quote":"{{Quote}}","wording":"Panes are at least 20 columns wide","decides":["{{Resize}}"]}]}""";
        var text = $"Pane resize: we agreed {Quote}, anything narrower is clamped.";
        var record = RecordSamples.Record("doc:harbor/plans/pane-resize.md", source: LibrarySource.Id) with
        {
            Kind = RecordKind.Doc,
            Title = "Pane resize limits",
            Audience = AudienceKey.From(audience),
            TextHash = RecordSamples.Hash(text),
        };
        var committed = await world.Records.Commit(RecordSamples.Batch(LibrarySource.Id, 1, RecordSamples.Write(record, text)), Ct);
        Assert.True(committed.IsSuccess, committed.IsError ? committed.Error.Message : null);

        var scrubbers = new FakeScrubberFactory().For([]).Value;
        var after = await world.Service<FeedDecisions>().For(MapWorld.Human, (new ContextSettings { LibraryModelRoots = modelRoots }, scrubbers), Ct);
        var step = after(new FakeRecordFeed(LibrarySource.Id, []));
        if (step is not null)
        {
            _ = await step(new SourceSync(LibrarySource.Id, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, Code: null));
        }

        return world.Model.Requests.Count(r => r.Purpose == ModelPurpose.DecisionExtraction);
    }

    [Fact]
    public async Task A_library_record_is_not_sent_to_the_decision_model_by_default() =>
        Assert.Equal(0, await Extractions([]));

    [Fact]
    public async Task A_library_record_is_sent_once_when_its_root_is_listed() =>
        Assert.Equal(1, await Extractions(["harbor"]));

    [Fact]
    public async Task A_library_record_of_another_root_is_still_withheld() =>
        Assert.Equal(0, await Extractions(["warehouse"]));

    [Fact]
    public async Task An_owner_library_doc_is_withheld_from_the_decision_model_even_when_its_root_opted_in() =>
        Assert.Equal(0, await Extractions(["harbor"], audience: "owner:library"));   // a decision carries a verbatim quote that a session's scoped map would show
}
