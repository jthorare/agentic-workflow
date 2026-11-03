using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Testing;
using Aw.Judge.Tests.Images;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Tests.SortPrompt;

// The prompt-sort.sh hook path with images, through the real command line: the stdin record's `images` reaches Jev's batch as an image
// and the decision row's stored input keeps it. Nothing else proves the real command reads the field off stdin.
public sealed class SortPromptImagesCliTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task The_hook_path_reads_images_off_stdin_and_the_decision_row_keeps_them()
    {
        var png = Path.Combine(_state.Path, "shot.png");
        await File.WriteAllBytesAsync(png, ImageBytes.Png(1512, 982, "shot"), TestContext.Current.CancellationToken);
        var log = new InMemoryDecisionLog();
        var jev = FakeBatchClassifier.Unavailable();
        var cli = new JudgeCli(_state) { Overrides = services => services.AddSingleton<IDecisionLog>(log).AddSingleton<IBatchClassifier>(jev) };
        var stdin = JsonSerializer.Serialize(new { prompt = "make the page look like the screenshot", sessionId = "sess-1", images = new[] { png }, imagesBestEffort = true });

        var run = await cli.RunWithInput(stdin, "prompt-sort");

        Assert.Equal(0, run.ExitCode);
        var batch = Assert.Single(jev.Asked);
        Assert.Equal(png, Assert.Single(batch.Images).Path);
        Assert.True(batch.ImagesBestEffort);
        var stored = JsonNode.Parse(Assert.Single(log.Decisions).Details!.InputJson)!;   // why: the row always holds JSON
        Assert.Equal(png, stored["images"]?[0]?["path"]?.GetValue<string>());
    }

    public void Dispose() => _state.Dispose();
}
