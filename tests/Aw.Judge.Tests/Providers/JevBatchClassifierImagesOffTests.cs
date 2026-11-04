using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Config;
using Aw.Judge.Infrastructure.Providers;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

/// <summary>By default a batch keeps its images; with providers.jevImages false or AW_JUDGE_JEV_IMAGES=0 it goes as text and says so, and the environment beats config.json both ways.</summary>
[Lane(TestLane.Unit)]
public sealed class JevBatchClassifierImagesOffTests
{
    private const string Answer = """{"answers":{"touchesUi":{"type":"noul","noul":0.9}},"usage":{"input_tokens":12,"output_tokens":1}}""";

    private static readonly NoulBatchQuestion _noul = new("touchesUi", "Does the work change the UI?");

    [Fact]
    public async Task By_default_a_batch_keeps_its_images_with_no_off_note()
    {
        var handler = new RecordingJevHandler(_ => Answer);
        using var jev = JevImages.Jev(handler);

        var result = await new JevBatchClassifier(jev, _ => null).Classify(Batch(), providers: null, allowed: null, TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(("true", "multipart/form-data"), (request.ImagesHeader, request.ContentType));
        Assert.Null(result.ImageNotes);
    }

    [Fact]
    public async Task Images_off_in_config_a_batch_goes_as_text_with_the_off_note()
    {
        var handler = new RecordingJevHandler(_ => Answer);
        using var jev = JevImages.Jev(handler);

        var result = await new JevBatchClassifier(jev, _ => null).Classify(Batch(), new ProvidersConfig { JevImages = false }, allowed: null, TestContext.Current.CancellationToken);

        _ = Assert.IsType<BatchDecided>(result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(("application/json", null), (request.ContentType, request.ImagesHeader));
        Assert.Equal("image sending to Jev is off (providers.jevImages false or AW_JUDGE_JEV_IMAGES=0); asked jev as text", Assert.Single(result.ImageNotes ?? []));
    }

    [Fact]
    public async Task Images_off_in_the_environment_beats_a_config_that_turns_them_on()
    {
        var handler = new RecordingJevHandler(_ => Answer);
        using var jev = JevImages.Jev(handler);
        var on = new ProvidersConfig { JevImages = true };

        _ = await new JevBatchClassifier(jev, name => name == "AW_JUDGE_JEV_IMAGES" ? "0" : null).Classify(Batch(), on, allowed: null, TestContext.Current.CancellationToken);

        Assert.Equal("application/json", Assert.Single(handler.Requests).ContentType);
    }

    [Fact]
    public async Task Images_off_in_the_environment_with_no_config_a_batch_goes_as_text_with_the_off_note()
    {
        // The hooks' opt-out: they reach the batch path, which reads AW_JUDGE_JEV_IMAGES itself.
        var handler = new RecordingJevHandler(_ => Answer);
        using var jev = JevImages.Jev(handler);

        var result = await new JevBatchClassifier(jev, name => name == "AW_JUDGE_JEV_IMAGES" ? "0" : null).Classify(Batch(), providers: null, allowed: null, TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(("application/json", null), (request.ContentType, request.ImagesHeader));
        Assert.Equal("image sending to Jev is off (providers.jevImages false or AW_JUDGE_JEV_IMAGES=0); asked jev as text", Assert.Single(result.ImageNotes ?? []));
    }

    [Fact]
    public async Task Images_on_in_the_environment_beats_a_config_that_turns_them_off()
    {
        var handler = new RecordingJevHandler(_ => Answer);
        using var jev = JevImages.Jev(handler);
        var off = new ProvidersConfig { JevImages = false };

        var result = await new JevBatchClassifier(jev, name => name == "AW_JUDGE_JEV_IMAGES" ? "1" : null).Classify(Batch(), off, allowed: null, TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(("true", "multipart/form-data"), (request.ImagesHeader, request.ContentType));
        Assert.Null(result.ImageNotes);
    }

    [Theory]
    [InlineData("1")]
    [InlineData(null)]
    public async Task Images_on_in_the_environment_or_in_config_the_batch_carries_the_image(string? environmentValue)
    {
        var handler = new RecordingJevHandler(_ => Answer);
        using var jev = JevImages.Jev(handler);
        var config = environmentValue is null ? new ProvidersConfig { JevImages = true } : null;

        var result = await new JevBatchClassifier(jev, name => name == "AW_JUDGE_JEV_IMAGES" ? environmentValue : null).Classify(Batch(), config, allowed: null, TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(("true", "multipart/form-data"), (request.ImagesHeader, request.ContentType));
        Assert.Null(result.ImageNotes);
    }

    private static BatchRequest Batch() =>
        new(State: null, [_noul], TimeSpan.FromSeconds(10)) { Images = [JevImages.Attachment("/ev/home.png", 1512, 982)], ImagesBestEffort = true };
}
