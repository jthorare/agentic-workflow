using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Infrastructure.Judge;
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

    [Fact]
    public async Task JudgeChoose_fails_the_step_when_a_directory_symlink_in_the_checkout_points_out()
    {
        _ = File(Path.Combine(Elsewhere, "downloads"), "x.png");
        _ = Directory.CreateDirectory(Checkout);
        _ = Directory.CreateSymbolicLink(Path.Combine(Checkout, "shots"), Path.Combine(Elsewhere, "downloads"));
        var path = Path.Combine(Checkout, "shots", "x.png");
        var judge = Decided();

        var result = await Run(judge, path);

        Assert.Empty(judge.Requests);
        Assert.Equal($"step.judge.choose: images: {path} (from fact:frames) is outside the checkout and the ui-evidence output", result.Failure);
    }

    [Fact]
    public async Task JudgeChoose_fails_the_step_when_the_facts_own_dotdot_climbs_out_of_a_linked_directory()
    {
        _ = File(Path.Combine(Elsewhere, "a"), "x.png");
        _ = Directory.CreateDirectory(Path.Combine(Elsewhere, "a", "b"));
        _ = Directory.CreateDirectory(Checkout);
        _ = Directory.CreateSymbolicLink(Path.Combine(Checkout, "shots"), Path.Combine(Elsewhere, "a", "b"));
        var path = Path.Combine(Checkout, "shots", "..", "x.png");   // the OS opens elsewhere/a/x.png; read as text it would be checkout/x.png
        var judge = Decided();

        var result = await Run(judge, path);

        Assert.Empty(judge.Requests);
        Assert.Equal($"step.judge.choose: images: {path} (from fact:frames) is outside the checkout and the ui-evidence output", result.Failure);
    }

    [Fact]
    public async Task JudgeChoose_fails_the_step_when_a_relative_file_link_inside_a_linked_directory_climbs_out()
    {
        // checkout/indir -> elsewhere/out/inner, and inner/x.png -> ../secret.png: read as text that is checkout/secret.png, but the real file is elsewhere/out/secret.png.
        _ = File(Path.Combine(Elsewhere, "out"), "secret.png");
        _ = Directory.CreateDirectory(Path.Combine(Elsewhere, "out", "inner"));
        _ = System.IO.File.CreateSymbolicLink(Path.Combine(Elsewhere, "out", "inner", "x.png"), Path.Combine("..", "secret.png"));
        _ = Directory.CreateDirectory(Checkout);
        _ = Directory.CreateSymbolicLink(Path.Combine(Checkout, "indir"), Path.Combine(Elsewhere, "out", "inner"));
        var path = Path.Combine(Checkout, "indir", "x.png");
        var judge = Decided();

        var result = await Run(judge, path);

        Assert.Empty(judge.Requests);
        Assert.Equal($"step.judge.choose: images: {path} (from fact:frames) is outside the checkout and the ui-evidence output", result.Failure);
    }

    [Fact]
    public async Task JudgeChoose_takes_an_image_under_a_directory_symlink_that_stays_inside_the_checkout()
    {
        _ = File(Path.Combine(Checkout, "real"), "home.png");
        _ = Directory.CreateSymbolicLink(Path.Combine(Checkout, "alias"), Path.Combine(Checkout, "real"));
        var path = Path.Combine(Checkout, "alias", "home.png");
        var judge = Decided();

        var result = await Run(judge, path);

        Assert.Null(result.Failure);
        Assert.Equal([path], Assert.Single(judge.Requests).Images, StringComparer.Ordinal);
    }

    private static FakeJudgeChoice Decided() => new FakeJudgeChoice().Answers(new ChooseAnswer(ChooseStatus.Decided, "api", "model", 0.9, "jev", "d-1"));

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
        return new JudgeChooseStep(judge, new PassThroughScrubber(), new FakeStepImageRoots(Checkout, Evidence), new StepImageFiles()).Run(context, TestContext.Current.CancellationToken).AsTask();
    }
}
