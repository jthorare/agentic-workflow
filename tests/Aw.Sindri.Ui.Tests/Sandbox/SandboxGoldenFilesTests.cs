using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Sandbox;

/// <summary>The sandbox goldens reach the UI test output and parse, so SB-6's view-model tests can read them (sandbox spec §8; SB-0).</summary>
[Lane(TestLane.Unit)]
public sealed class SandboxGoldenFilesTests
{
    private static readonly string[] _json = ["copy.json", "states.json", "accessibility.json", "doctor.json", "code-kinds.json", "shims.json"];
    private static readonly string[] _text = ["statusline.txt", "statusline-attended.txt", "takeover-banner.txt", "refusal.txt"];

    [Fact]
    public void Every_sandbox_json_golden_arrives_and_parses() =>
        Assert.All(_json, name => Assert.IsType<JsonObject>(JsonNode.Parse(GoldenFiles.Sandbox(name))));

    [Fact]
    public void Every_sandbox_text_golden_arrives() =>
        Assert.All(_text, name => Assert.False(string.IsNullOrWhiteSpace(GoldenFiles.Sandbox(name))));

    [Fact]
    public void The_park_card_name_starts_from_the_first_clause_of_a_golden_sentence()
    {
        var entries = JsonNode.Parse(GoldenFiles.Sandbox("copy.json"))!["entries"]!.AsArray();   // why: the golden is a JSON object with an entries array
        var first = (string)entries.First(e => (string)e!["reason"]! == "no-bwrap")!["text"]!;

        Assert.StartsWith("This machine has no sandbox tool", first, StringComparison.Ordinal);
    }
}
