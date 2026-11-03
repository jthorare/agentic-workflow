using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Questions;
using Aw.Judge.Infrastructure.Images;
using Aw.Judge.Infrastructure.Providers;
using Aw.Judge.Testing;
using Aw.Judge.Tests.Images;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;
using static Aw.Judge.Tests.Questions.QuestionRun;

namespace Aw.Judge.Tests.Questions;

/// <summary>
/// visual-critique's two screenshots are read once into attachments, before any provider runs, and the agent CLIs see byte-identical
/// copies of them in a per-call directory under the same relative names. Real files and symlinks on disk, so the integration lane.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class VisualCritiqueImageTests
{
    private const string Prefix = "aw-judge-images-";

    private readonly FakeProcessRunner _runner = new();

    [Fact]
    public async Task VisualCritique_cli_sees_the_same_screenshots_in_the_same_order()
    {
        using var evidence = new ScratchDirectory();
        var after = ImageBytes.Png(1512, 982, "after");
        var before = ImageBytes.Png(1512, 982, "before");
        _ = evidence.Write("after.png", after);
        _ = evidence.Write("base/before.png", before);
        string? directory = null;
        byte[]? seenAfter = null;
        byte[]? seenBefore = null;
        string[]? seenFiles = null;
        Install(request =>
        {
            directory = request.WorkingDirectory;
            seenFiles = [.. Directory.EnumerateFiles(directory!, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(directory!, f).Replace('\\', '/')).Order(StringComparer.Ordinal)];
            seenAfter = File.ReadAllBytes(Path.Combine(directory!, "after.png"));
            seenBefore = File.ReadAllBytes(Path.Combine(directory!, "base", "before.png"));
        });

        var outcome = await Ask(evidence.Path, "after.png", "base/before.png");

        _ = AssertModel("looks-right", outcome);
        var call = Assert.Single(_runner.Calls).Request;
        Assert.NotEqual(evidence.Path, directory, StringComparer.Ordinal);
        Assert.StartsWith(Prefix, Path.GetFileName(directory), StringComparison.Ordinal);
        Assert.Equal(["after.png", "base/before.png"], seenFiles ?? []);
        Assert.Equal(after, seenAfter);
        Assert.Equal(before, seenBefore);
        var prompt = call.StandardInput!;
        Assert.Contains("Then look at base/before.png", prompt, StringComparison.Ordinal);
        Assert.True(prompt.IndexOf("after.png", StringComparison.Ordinal) < prompt.IndexOf("base/before.png", StringComparison.Ordinal), prompt);
    }

    [Fact]
    public async Task VisualCritique_symlink_out_of_evidence_dir_is_refused()
    {
        using var evidence = new ScratchDirectory();
        using var elsewhere = new ScratchDirectory();
        var secret = elsewhere.Write("secret.png", ImageBytes.Png(100, 100));
        _ = File.CreateSymbolicLink(Path.Combine(evidence.Path, "after.png"), secret);
        Install(_ => { });

        var outcome = await Ask(evidence.Path, "after.png", baseline: null);

        Assert.Equal(new EscalatedOutcome("invalid-input"), outcome);
        Assert.Empty(_runner.Calls);
    }

    [Fact]
    public async Task VisualCritique_missing_screenshot_escalates_invalid_input()
    {
        using var evidence = new ScratchDirectory();
        Install(_ => { });

        var outcome = await Ask(evidence.Path, "after.png", baseline: null);

        Assert.Equal(new EscalatedOutcome("invalid-input"), outcome);
        Assert.Empty(_runner.Calls);
    }

    [Fact]
    public async Task ImageWorkspace_is_removed_after_the_call()
    {
        using var evidence = new ScratchDirectory();
        _ = evidence.Write("after.png", ImageBytes.Png(1512, 982));
        string? directory = null;
        var existedDuringTheCall = false;
        Install(request =>
        {
            directory = request.WorkingDirectory;
            existedDuringTheCall = Directory.Exists(directory);
        });

        _ = await Ask(evidence.Path, "after.png", baseline: null);

        Assert.True(existedDuringTheCall);
        Assert.StartsWith(Prefix, Path.GetFileName(directory), StringComparison.Ordinal);
        Assert.False(Directory.Exists(directory), directory);
    }

    // claude answers looks-right; the hook sees the request as the run starts, while the per-call directory still exists.
    private void Install(Action<Core.Processes.ProcessRequest> onRun) =>
        _runner.With("claude", request =>
        {
            onRun(request);
            return new FakeProgram { StandardOutput = Providers.Questions.ClaudeStructured("""{"decision":"looks-right","reasons":[]}""") };
        });

    private ValueTask<EvaluationOutcome> Ask(string evidenceDir, string after, string? baseline)
    {
        var input = new System.Text.Json.Nodes.JsonObject { ["afterScreenshot"] = after, ["baselineScreenshot"] = baseline, ["evidenceDir"] = evidenceDir };
        var evaluator = new QuestionEvaluator(Log, new FakeInputRedactor(), new SequentialIdGenerator(), new FakeTimeProvider(KnownInstants.Epoch), Json);
        var context = new EvaluationContext { Providers = [new ClaudeCliProvider(_runner)], ImageReader = new FileImageReader(), ImageWorkspaces = new ImageWorkspaces() };
        return evaluator.Evaluate(VisualCritiqueQuestion.Definition, input.ToJsonString(), context, TestContext.Current.CancellationToken);
    }

    private InMemoryDecisionLog Log { get; } = new();
}
