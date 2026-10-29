using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Models;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Features.SyncSources;
using Aw.Context.Testing.ConceptSynthesis;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Context.Testing.Vectors;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.SynthesizeConcepts;

/// <summary>
/// The synthesis run over the fakes. Records sit on a 2-D unit circle, so the angle between two of them is their distance: records a few degrees apart are one concept, records 90 degrees apart are not.
/// </summary>
internal sealed class SynthesisRig
{
    private int _page;

    public SynthesisRig()
    {
        Model.Responder = request => Answer?.Invoke(request) ?? Json(Name, Summary);
    }

    public FakeRecordStore Records { get; } = new();

    public FakeConceptSynthesisStore Concepts { get; } = new();

    public FakeModelPort Model { get; } = new();

    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-10-29T12:00:00Z", CultureInfo.InvariantCulture));

    /// <summary>The name the model gives every group, unless <see cref="Answer"/> is set.</summary>
    public string Name { get; set; } = "Checkout retry";

    /// <summary>The summary the model gives every group, unless <see cref="Answer"/> is set.</summary>
    public string Summary { get; set; } = "Orders are retried when the payment call times out.";

    /// <summary>The model's answer to a request, or null for the JSON of <see cref="Name"/> and <see cref="Summary"/>.</summary>
    public Func<ModelRequest, string>? Answer { get; set; }

    public int Asked => Model.Requests.Count(r => r.Purpose == ModelPurpose.ConceptSynthesis);

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static string Json(string name, string summary) => $$"""{"name":"{{name}}","summary":"{{summary}}"}""";

    /// <summary>Stores a record with one chunk and its vector at an angle on the circle.</summary>
    public async Task Add(string source, string key, double degrees, string? text = null, string version = "v1")
    {
        var record = RecordSamples.Record(key, version, SourceId.From(source));
        var body = text ?? ("text of " + key);
        _ = await Records.Commit(RecordSamples.Batch(record.Source, ++_page, RecordSamples.Write(record, body)), Ct);
        var radians = degrees * Math.PI / 180;
        _ = await Records.PutVectors([RecordSamples.Vector(new ChunkKey(record.Key, 0), body, (float)Math.Cos(radians), (float)Math.Sin(radians))], Ct);
    }

    /// <summary>Stores a record whose chunk has no vector yet.</summary>
    public async Task AddUnembedded(string source, string key)
    {
        var record = RecordSamples.Record(key, "v1", SourceId.From(source));
        _ = await Records.Commit(RecordSamples.Batch(record.Source, ++_page, RecordSamples.Write(record, "text of " + key)), Ct);
    }

    /// <summary>Tombstones a record: the source deleted it.</summary>
    public async Task Remove(string source, string key) =>
        _ = await Records.Commit(new RecordBatch(SourceId.From(source), [], [RecordKey.From(key)], SyncPosition.Start, Clock.GetUtcNow()), Ct);

    public ValueTask<ConceptSynthesisReport> Run(int maxTokens = 50_000, ConceptSynthesisSettings? settings = null) =>
        new ConceptSynthesisRun(Records, new FakeVectorScan(), Concepts, Model, Clock).Run(settings ?? new ConceptSynthesisSettings(), new LabelingSettings { MaxTokensPerSync = maxTokens }, Ct);
}
