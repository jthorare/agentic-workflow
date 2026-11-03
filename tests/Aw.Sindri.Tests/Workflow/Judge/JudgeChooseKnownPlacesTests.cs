using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Judge;

namespace Aw.Sindri.Tests.Workflow.Judge;

/// <summary>
/// An image of 'step.judge.choose' must resolve (symlinks followed) under the item's checkout or the ui-evidence output (images spec 3.4): anything else
/// fails the Step before any judge call, because Jev is a third party. Real files and a symlink in temp directories, so the integration lane.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class JudgeChooseKnownPlacesTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("aw-judge-choose-places-").FullName;

    private string Checkout => Path.Combine(_root, "checkout");

    private string Evidence => Path.Combine(_root, "state", "ui-evidence");

    private string Elsewhere => Path.Combine(_root, "elsewhere");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task JudgeChoose_fails_the_step_when_an_image_is_outside_the_checkout_and_the_ui_evidence_output()
    {
        var outside = File(Elsewhere, "secret.png");
        var link = Path.Combine(Checkout, "looks-inside.png");
        _ = Directory.CreateDirectory(Checkout);
        _ = System.IO.File.CreateSymbolicLink(link, outside);

        foreach (var path in new[] { outside, link })
        {
            var judge = new FakeJudgeChoice().Answers(new ChooseAnswer(ChooseStatus.Decided, "api", "model", 0.9, "jev", "d-1"));

            var result = await Run(judge, path);

            Assert.Empty(judge.Requests);
            Assert.Equal($"step.judge.choose: images: {path} (from fact:frames) is outside the checkout and the ui-evidence output", result.Failure);
        }
    }

    [Fact]
    public async Task JudgeChoose_takes_an_image_under_the_checkout_or_the_ui_evidence_output()
    {
        var inCheckout = File(Checkout, "home.png");
        var inEvidence = File(Path.Combine(Evidence, "runs", "r1"), "home.png");
        var judge = new FakeJudgeChoice().Answers(new ChooseAnswer(ChooseStatus.Decided, "api", "model", 0.9, "jev", "d-1"));

        var result = await Run(judge, inCheckout, inEvidence);

        Assert.Null(result.Failure);
        Assert.Equal([inCheckout, inEvidence], Assert.Single(judge.Requests).Images, StringComparer.Ordinal);
    }

    private static string File(string directory, string name)
    {
        _ = Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        System.IO.File.WriteAllBytes(path, [0x89, 0x50, 0x4E, 0x47]);
        return path;
    }

    private Task<BlockStepResult> Run(FakeJudgeChoice judge, params string[] frames)
    {
        var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["images"] = JsonSerializer.SerializeToElement("fact:frames") };
        var context = JudgeChooseStepTests.Context(arguments, ("fact:built", "{\"head\":\"abc\"}"), ("fact:frames", JsonSerializer.Serialize(frames)));
        return new JudgeChooseStep(judge, new PassThroughScrubber(), new FakeStepImageRoots(Checkout, Evidence)).Run(context, TestContext.Current.CancellationToken).AsTask();
    }
}
