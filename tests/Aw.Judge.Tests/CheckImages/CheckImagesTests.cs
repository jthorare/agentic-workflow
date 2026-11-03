using System.Text;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Images;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Tests.Images;
using Aw.Judge.Tests.Providers;
using Aw.Kernel.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Tests.CheckImages;

/// <summary>
/// 'judge check images': the whole Jev-against-the-CLI check in one command (images spec, 8). A recording Jev that calls a page broken when its
/// screenshot is named so, a stub CLI that does the same, and four tiny synthetic cases injected through IImageCaseSet in place of the 20 embedded ones.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class CheckImagesTests : IDisposable
{
    private const string Config = """{"questions":{},"providers":{"jevImages":false}}""";

    private const string Scores = @"broken judged looks-right 0/2 · right pages judged looks-right 2/2 · \d+(\.\d+)? s per call$";

    private readonly TempStateDirectory _state = new();

    public void Dispose() => _state.Dispose();

    [Fact]
    public async Task CheckImages_prints_both_scores_and_the_flip_command_when_the_bar_holds()
    {
        var configFile = Path.Combine(_state.Path, "judge", "config.json");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(configFile)!);   // why: the path has a directory part by construction
        await File.WriteAllTextAsync(configFile, Config, TestContext.Current.CancellationToken);
        var handler = new RecordingJevHandler(request => JevImages.Choice(
            request.Parts.Any(p => p.Name != "request" && Encoding.ASCII.GetString(p.Bytes).Contains("broken", StringComparison.Ordinal)) ? "looks-off" : "looks-right"));
        using var jev = JevImages.Jev(handler);
        var cases = new[] { Case("right-1", "looks-right"), Case("right-2", "looks-right"), Case("broken-1", "looks-off"), Case("broken-2", "looks-off") };
        var cli = new JudgeCli(_state)
        {
            Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["TYPESAFE_API_KEY"] = JevImages.Key },
            Providers = [jev, new StubCli()],
            Overrides = services => services.AddSingleton<IImageCaseSet>(new CaseSet(cases)),
        };

        var run = await cli.Run("check", "images");

        Assert.Equal((0, ""), (run.ExitCode, run.Stderr));
        var lines = run.Stdout.TrimEnd('\n').Split('\n');
        Assert.Equal(6, lines.Length);
        Assert.Matches(@"^live call:  looks-right  provider jev  \(judge why \S+\)$", lines[0]);
        Assert.Matches("^jev:        " + Scores, lines[1]);
        Assert.Matches("^claude-cli: " + Scores, lines[2]);
        Assert.Equal("bar (0 broken judged looks-right): held", lines[3]);
        Assert.Equal("images to Jev: off", lines[4]);
        Assert.Equal("to turn it on: judge config set providers jev-images true", lines[5]);
        Assert.Equal(Config, await File.ReadAllTextAsync(configFile, TestContext.Current.CancellationToken));
        Assert.Equal(5, handler.Requests.Count);   // one live call, then one per case
        Assert.All(handler.Requests, request => Assert.Equal("true", request.ImagesHeader));
    }

    [Fact]
    public async Task CheckImages_counts_a_case_over_jevs_size_limit_as_skipped_not_wrong()
    {
        var handler = new RecordingJevHandler(request => JevImages.Choice(
            request.Parts.Any(p => p.Name != "request" && Encoding.ASCII.GetString(p.Bytes).Contains("broken", StringComparison.Ordinal)) ? "looks-off" : "looks-right"));
        using var jev = JevImages.Jev(handler);
        var retina = new ImageCase { Name = "retina-right", Expected = "looks-right", Bytes = ImageBytes.Png(3024, 1964, "retina-right") };
        var cases = new[] { Case("right-1", "looks-right"), Case("right-2", "looks-right"), Case("broken-1", "looks-off"), Case("broken-2", "looks-off"), retina };
        var cli = new JudgeCli(_state)
        {
            Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["TYPESAFE_API_KEY"] = JevImages.Key },
            Providers = [jev, new StubCli()],
            Overrides = services => services.AddSingleton<IImageCaseSet>(new CaseSet(cases)),
        };

        var run = await cli.Run("check", "images");

        Assert.Equal((0, ""), (run.ExitCode, run.Stderr));
        var lines = run.Stdout.Split('\n');
        Assert.Matches(@"^jev:        broken judged looks-right 0/2 · right pages judged looks-right 2/2 · 1 over its size limits, skipped · \d+(\.\d+)? s per call$", lines[1]);
        Assert.Matches(@"^claude-cli: broken judged looks-right 0/2 · right pages judged looks-right 3/3 · \d+(\.\d+)? s per call$", lines[2]);
        Assert.Equal(5, handler.Requests.Count);   // the live call and the four that fit: the retina case never reaches Jev
    }

    [Fact]
    public async Task CheckImages_counts_a_looks_right_under_the_threshold_as_not_settled_like_the_chain_does()
    {
        // Jev says looks-right on every page, sure only of the right ones: the chain would pass the broken ones on to the CLI.
        var handler = new RecordingJevHandler(request => JevImages.Choice(
            "looks-right",
            request.Parts.Any(p => p.Name != "request" && Encoding.ASCII.GetString(p.Bytes).Contains("broken", StringComparison.Ordinal)) ? "0.3" : "0.9"));
        using var jev = JevImages.Jev(handler);
        var cases = new[] { Case("right-1", "looks-right"), Case("right-2", "looks-right"), Case("broken-1", "looks-off"), Case("broken-2", "looks-off") };
        var cli = new JudgeCli(_state)
        {
            Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["TYPESAFE_API_KEY"] = JevImages.Key },
            Providers = [jev, new StubCli()],
            Overrides = services => services.AddSingleton<IImageCaseSet>(new CaseSet(cases)),
        };

        var run = await cli.Run("check", "images");

        Assert.Equal((0, ""), (run.ExitCode, run.Stderr));
        Assert.Matches("^jev:        " + Scores, run.Stdout.Split('\n')[1]);
    }

    [Fact]
    public async Task An_erroring_jev_never_holds_the_image_bar()
    {
        // A bad key or Jev down: every call errors. That proves nothing about Jev's eye, so the bar is not held and nothing says to turn images on.
        var handler = new RecordingJevHandler(_ => "{}", System.Net.HttpStatusCode.InternalServerError);
        using var jev = JevImages.Jev(handler);
        var cases = new[] { Case("right-1", "looks-right"), Case("right-2", "looks-right"), Case("broken-1", "looks-off"), Case("broken-2", "looks-off") };
        var cli = new JudgeCli(_state)
        {
            Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["TYPESAFE_API_KEY"] = JevImages.Key },
            Providers = [jev, new StubCli()],
            Overrides = services => services.AddSingleton<IImageCaseSet>(new CaseSet(cases)),
        };

        var run = await cli.Run("check", "images");

        Assert.Equal((1, ""), (run.ExitCode, run.Stderr));
        var lines = run.Stdout.TrimEnd('\n').Split('\n');
        Assert.Matches(@"^jev:        broken judged looks-right 0/0 · right pages judged looks-right 0/0 · 4 errored · \d+(\.\d+)? s per call$", lines.Single(l => l.StartsWith("jev:", StringComparison.Ordinal)));
        Assert.Contains(lines, l => l.StartsWith("bar (0 broken judged looks-right): not proven", StringComparison.Ordinal));
        Assert.DoesNotContain("held", run.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("to turn it on", run.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckImages_stops_without_a_jev_key()
    {
        var cli = new JudgeCli(_state) { Overrides = services => services.AddSingleton<IImageCaseSet>(new CaseSet([Case("right-1", "looks-right")])) };

        var run = await cli.Run("check", "images");

        Assert.Equal((1, "no Jev key: judge check images needs one\n", ""), (run.ExitCode, run.Stdout, run.Stderr));
    }

    [Fact]
    public async Task CheckImages_stops_when_no_case_is_embedded()
    {
        using var jev = JevImages.Jev(new RecordingJevHandler(_ => JevImages.Choice("looks-right")));
        var cli = new JudgeCli(_state)
        {
            Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["TYPESAFE_API_KEY"] = JevImages.Key },
            Providers = [jev, new StubCli()],
            Overrides = services => services.AddSingleton<IImageCaseSet>(new CaseSet([])),
        };

        var run = await cli.Run("check", "images");

        Assert.Equal((1, "no image cases embedded\n", ""), (run.ExitCode, run.Stdout, run.Stderr));
    }

    private static ImageCase Case(string name, string expected) => new() { Name = name, Expected = expected, Bytes = ImageBytes.Png(400, 300, name) };

    private sealed class CaseSet(IReadOnlyList<ImageCase> cases) : IImageCaseSet
    {
        public IReadOnlyList<ImageCase> Cases { get; } = cases;
    }

    // The agent CLI: calls a screenshot broken when its file name says so, whatever order the cases are asked in.
    private sealed class StubCli : IModelProvider
    {
        public ProviderName Name => ProviderName.ClaudeCli;

        public IReadOnlySet<ContentClass> Classes { get; } = new HashSet<ContentClass> { ContentClass.Image };

        public ValueTask<ProviderResult> Decide(QuestionRef question, JsonNode? input, TimeSpan budget, CancellationToken cancellationToken)
        {
            var broken = input?["afterScreenshot"]?.GetValue<string>().Contains("broken", StringComparison.Ordinal) == true;
            ProviderResult result = broken
                ? new ProviderDecided("looks-off", 0.9, "model") { Extra = new JsonObject { ["reasons"] = new JsonArray("the header overlaps the title") } }
                : new ProviderDecided("looks-right", 0.9, "model");
            return ValueTask.FromResult(result);
        }
    }
}
