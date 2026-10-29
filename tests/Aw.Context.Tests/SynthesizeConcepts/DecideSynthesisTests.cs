using System.Globalization;
using Aw.Context.Core.Models;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.SynthesizeConcepts;

/// <summary>The index-decisions run (<see cref="Aw.Context.Core.Sync.IIndexSync.Decide"/>) reads each record for decisions first, then groups the whole store into concepts (#1597).</summary>
[Trait("Lane", "unit")]
public sealed class DecideSynthesisTests
{
    private const string Quote = "the minimum pane width is 20 columns";
    private const string Resize = "feature:layout.pane-resize";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RecordItem Issue(int number, DateTimeOffset now) => new(
        RecordKey.From(string.Create(CultureInfo.InvariantCulture, $"github:issue/acme/shop/{number}")), RecordKind.Issue, "Pane resize limits", $"Pane resize: we agreed {Quote}, anything narrower is clamped.", Author: null, Url: null, RecordState.Open, StateRaw: null,
        CreatedAt: null, now.AddDays(-1), ClosedAt: null, SourceVersion.From("v1"), Links: [], Repo: null, Audience: AudienceKey.From("repo:acme/shop"));

    private static string Respond(ModelRequest request)
    {
        if (request.Purpose == ModelPurpose.ConceptSynthesis)
        {
            return SynthesisRig.Json("Pane resizing", "How narrow a pane may get.");
        }

        return request.Purpose == ModelPurpose.Adjudication
            ? """{"verdict":"agrees"}"""
            : $$"""{"decisions":[{"quote":"{{Quote}}","wording":"Panes are at least 20 columns wide","decides":["{{Resize}}"]}]}""";
    }

    [Fact]
    public async Task Decide_runs_the_per_record_pass_and_then_synthesis()
    {
        var now = DateTimeOffset.Parse("2026-08-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);   // MapWorld's clock
        using var world = new MapWorld(feeds: new FakeRecordFeedCatalog(new FakeRecordFeed(RecordSamples.Github, [Issue(1, now), Issue(2, now)])));
        _ = await world.ImportFixture();
        world.Model.Responder = Respond;
        var scope = MapWorld.HumanWith("{\"labeling\":{\"enabled\":true}}");
        _ = await world.Engine.Tick(scope, Ct);
        foreach (var record in (await world.Records.List(new RecordFilter(), after: null, 10, Ct)).Value)
        {
            var chunk = Assert.Single((await world.Records.Chunks(record.Key, Ct)).Value);
            _ = await world.Records.PutVectors([RecordSamples.Vector(new ChunkKey(record.Key, 0), chunk.Text, 1f, 0f)], Ct);
        }

        var decided = await world.Engine.Decide(scope, Ct);

        Assert.True(decided.IsSuccess, decided.IsError ? decided.Error.Message : null);
        Assert.Equal(2, Assert.Single(decided.Value.Sources).Looked);
        var synthesis = decided.Value.Synthesis;
        Assert.NotNull(synthesis);
        Assert.Null(synthesis.Code);
        Assert.Equal((1, 1, 2), (synthesis.Labeled, synthesis.Concepts, synthesis.Embedded));
        var concept = Assert.Single((await world.ConceptSynthesis.All(Ct)).Value);
        Assert.Equal("pane-resizing", concept.Key);
        var purposes = world.Model.Requests.Select(r => r.Purpose).ToList();
        Assert.True(purposes.IndexOf(ModelPurpose.DecisionExtraction) < purposes.IndexOf(ModelPurpose.ConceptSynthesis), string.Join(", ", purposes));
    }
}
