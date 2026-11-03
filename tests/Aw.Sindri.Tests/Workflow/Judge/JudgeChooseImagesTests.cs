using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Judge;

namespace Aw.Sindri.Tests.Workflow.Judge;

/// <summary>
/// 'step.judge.choose' with the images parameter, a fact key, (images spec 5.1): the fact's paths go to the judge as the question's images, at most four,
/// a missing file fails the Step before any judge call, and 'fact:decision' says how many were sent. Real files in a temp directory, so the integration lane.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class JudgeChooseImagesTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("aw-judge-choose-images-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task JudgeChoose_passes_the_named_fact_as_images()
    {
        var paths = Files(2);
        var judge = Decided();

        var result = await Run(judge, paths);

        Assert.Equal("api", result.Result!.Outcome.Value);
        Assert.Equal(paths, Assert.Single(judge.Requests).Images, StringComparer.Ordinal);
        Assert.Equal(2, (int?)Decision(result)["imageCount"]);
        Assert.Null(Decision(result)["imagesDropped"]);
    }

    [Fact]
    public async Task JudgeChoose_keeps_the_first_four_images_and_says_how_many_it_left_out()
    {
        var paths = Files(6);
        var judge = Decided();

        var result = await Run(judge, paths);

        Assert.Equal(paths.Take(4), Assert.Single(judge.Requests).Images, StringComparer.Ordinal);
        Assert.Equal(4, (int?)Decision(result)["imageCount"]);
        Assert.Equal(2, (int?)Decision(result)["imagesDropped"]);
    }

    [Fact]
    public async Task JudgeChoose_fails_the_step_before_any_judge_call_when_an_image_does_not_exist()
    {
        var missing = Path.Combine(_directory, "home.png");
        var judge = Decided();

        var result = await Run(judge, [missing]);

        Assert.Empty(judge.Requests);
        Assert.Equal($"step.judge.choose: images: {missing} (from fact:frames) does not exist", result.Failure);
    }

    [Fact]
    public async Task JudgeChoose_without_images_adds_nothing_to_the_decision()
    {
        var judge = Decided();
        var context = JudgeChooseStepTests.Context();

        var result = await new JudgeChooseStep(judge, new PassThroughScrubber()).Run(context, Token);

        Assert.Empty(Assert.Single(judge.Requests).Images);
        Assert.Null(Decision(result)["imageCount"]);
    }

    private string[] Files(int count) =>
        [.. Enumerable.Range(1, count).Select(i =>
        {
            var path = Path.Combine(_directory, "frame-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".png");
            File.WriteAllBytes(path, [0x89, 0x50, 0x4E, 0x47]);
            return path;
        })];

    private static FakeJudgeChoice Decided() => new FakeJudgeChoice().Answers(new ChooseAnswer(ChooseStatus.Decided, "api", "model", 0.9, "jev", "d-1"));

    private static Task<BlockStepResult> Run(FakeJudgeChoice judge, string[] frames)
    {
        var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["images"] = JsonSerializer.SerializeToElement("fact:frames") };
        var context = JudgeChooseStepTests.Context(arguments, ("fact:built", "{\"head\":\"abc\"}"), ("fact:frames", JsonSerializer.Serialize(frames)));
        return new JudgeChooseStep(judge, new PassThroughScrubber()).Run(context, Token).AsTask();
    }

    private static JsonObject Decision(BlockStepResult result) =>
        (JsonObject)JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:decision")].GetRawText())!;
}
