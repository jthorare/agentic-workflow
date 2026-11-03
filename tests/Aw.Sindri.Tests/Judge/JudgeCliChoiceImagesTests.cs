using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Infrastructure.Judge;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Tests.Judge;

/// <summary>What <see cref="JudgeCliChoice"/> writes on the judge's standard input: today's bytes for a text question (images spec 6), and the images when there are some.</summary>
[Lane(TestLane.Unit)]
public sealed class JudgeCliChoiceImagesTests
{
    private sealed class Runner : IToolRunner
    {
        public string? StandardInput { get; private set; }

        public ToolRun Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, ToolRunOptions? options = null, CancellationToken cancellationToken = default)
        {
            StandardInput = options?.StandardInput;
            return new ToolRun(ToolOutcome.Exited, 2, """{"escalated":true,"reasonCode":"below-threshold","id":"d-1"}""");
        }
    }

    private static readonly ChooseRequest _request = new(
        "Which evidence?",
        [new ChooseOption("ui", "a user sees it"), new ChooseOption("api", "a contract changed")],
        new Dictionary<string, string>(StringComparer.Ordinal) { ["fact:built"] = "built it" },
        0.7,
        ["claude-cli", "rules"]);

    private static async Task<string> Sent(ChooseRequest request)
    {
        var runner = new Runner();
        _ = await new JudgeCliChoice(runner, _ => "/bin/judge", "/home/sindri/judge").Choose(request, TestContext.Current.CancellationToken);
        return runner.StandardInput!;
    }

    /// <summary>Recorded from the code before any image field: green now, and it has to stay green.</summary>
    [Fact]
    public async Task JudgeCliChoice_without_images_sends_todays_input() =>
        Assert.Equal(
            """{"prompt":"Which evidence?","state":{"fact:built":"built it"},"choices":[{"name":"ui","criterion":"a user sees it"},{"name":"api","criterion":"a contract changed"}],"threshold":0.7}""",
            await Sent(_request));

    [Fact]
    public async Task JudgeCliChoice_with_images_sends_them_as_images_in_order()
    {
        var input = JsonNode.Parse(await Sent(_request with { Images = ["/home/u/r/ui-evidence/home.png", "/home/u/r/ui-evidence/home-base.png"] }))!;

        Assert.Equal(["/home/u/r/ui-evidence/home.png", "/home/u/r/ui-evidence/home-base.png"], input["images"]!.AsArray().Select(n => (string?)n), StringComparer.Ordinal);
        Assert.Equal("Which evidence?", (string?)input["prompt"]);
    }
}
