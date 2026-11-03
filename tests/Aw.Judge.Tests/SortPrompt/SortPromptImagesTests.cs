using System.Text.Json.Nodes;
using Aw.Judge.Features.SortPrompt;
using Aw.Judge.Host;
using Aw.Judge.Testing;
using Aw.Judge.Tests.Images;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.SortPrompt;

/// <summary>An image the prompt names reaches Jev's batch request, best effort, and the sort's decision row keeps what was sent and what was dropped (spec 3.6, 5.2).</summary>
[Lane(TestLane.Unit)]
public sealed class SortPromptImagesTests
{
    [Fact]
    public async Task SortPrompt_an_image_named_in_the_input_reaches_the_batch_as_an_image_and_the_row_keeps_it()
    {
        var png = ImageBytes.Png(1512, 982, "shot");
        var harness = new SortPromptHarness(SortPromptHarness.JevSaying(new Dictionary<string, double>(StringComparer.Ordinal)));
        var service = Service(harness, new FakeImageReader(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["/ev/shot.png"] = png }));

        _ = await service.Handle(A.SortPromptRequest().Build() with { Images = ["/ev/shot.png", "/ev/missing.png"] }, TestContext.Current.CancellationToken);

        var batch = Assert.Single(harness.Jev.Asked);
        var image = Assert.Single(batch.Images);
        Assert.Equal("/ev/shot.png", image.Path);
        Assert.Equal(png, image.Bytes.ToArray());
        Assert.True(batch.ImagesBestEffort);
        var stored = JsonNode.Parse(Assert.Single(harness.Log.Decisions).Details!.InputJson)!;   // why: the row always holds JSON
        Assert.Equal("/ev/shot.png", stored["images"]?[0]?["path"]?.GetValue<string>());
        Assert.Contains("/ev/missing.png", Assert.Single(stored["imagesDropped"]!.AsArray())?.GetValue<string>(), StringComparison.Ordinal);   // why: a drop is recorded
    }

    [Fact]
    public async Task SortPrompt_without_images_sends_no_images_and_keeps_the_row_as_it_was()
    {
        var harness = new SortPromptHarness(SortPromptHarness.JevSaying(new Dictionary<string, double>(StringComparer.Ordinal)));

        _ = await Service(harness, new FakeImageReader()).Handle(A.SortPromptRequest().Build(), TestContext.Current.CancellationToken);

        var batch = Assert.Single(harness.Jev.Asked);
        Assert.Empty(batch.Images);
        Assert.False(batch.ImagesBestEffort);
        var stored = JsonNode.Parse(Assert.Single(harness.Log.Decisions).Details!.InputJson)!;   // why: the row always holds JSON
        Assert.Equal(["prompt"], stored.AsObject().Select(pair => pair.Key), StringComparer.Ordinal);
    }

    private static SortPromptService Service(SortPromptHarness harness, FakeImageReader reader)
    {
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var recorder = new SortRecorder(harness.Log, harness.Store, new FakeInputRedactor(), new SequentialIdGenerator(), clock, AwJson.CreateOptions(JudgeJsonContext.Default));
        return new SortPromptService(harness.Config, harness.Jev, harness.Sessions, recorder, clock, reader);
    }
}
