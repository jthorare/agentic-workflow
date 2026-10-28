using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Sandbox;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Sandbox;

namespace Aw.Sindri.Ui.Tests.Sandbox;

/// <summary>The park card (sandbox spec §8 States, Park card; accessibility row Park card).</summary>
[Lane(TestLane.Unit)]
public sealed class SandboxParkCardTests
{
    private readonly SandboxTestBed _bed = new();

    private static SandboxBlockerLineView Blocker(string text, string? state = null) => new(Code: "SND-LAUNCH-014", Reason: "no-bwrap", Source: null, Text: text, Command: "sudo apt install bubblewrap", State: state);

    private static IEnumerable<JsonNode> Entries() => JsonNode.Parse(GoldenFiles.Sandbox("copy.json"))!["entries"]!.AsArray().Select(e => e!);

    [Fact]
    public void The_name_is_needs_you_the_step_and_the_first_clause_never_the_code()
    {
        var text = (string)Entries().First(e => (string)e["reason"]! == "no-bwrap")["text"]!;
        var card = new SandboxParkCardViewModel(Blocker(text), "Try the engine", _bed.Check);

        var expected = SandboxTestBed.Text("parkCard", "never-checked").Replace("<x>", "Try the engine", StringComparison.Ordinal).Replace("<first clause of the golden sentence>", "This machine has no sandbox tool", StringComparison.Ordinal);
        Assert.Equal(expected, card.Name);
        Assert.DoesNotContain("SND-", card.Name, StringComparison.Ordinal);
        Assert.DoesNotContain("no-bwrap", card.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_golden_sentence_has_a_first_clause_that_starts_it_and_names_no_code()
    {
        foreach (var entry in Entries())
        {
            var text = (string)entry["text"]!;
            var clause = SandboxParkCardViewModel.FirstClause(text);

            Assert.True(clause.Length > 0, text);
            Assert.StartsWith(clause, text.Replace("`", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
            Assert.True(clause.Length < text.Replace("`", string.Empty, StringComparison.Ordinal).Length, text);
            Assert.DoesNotContain("`", clause, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("The system blocks the sandbox tool. Allow bubblewrap in AppArmor, then press Check again.", "The system blocks the sandbox tool")]
    [InlineData("Steps can't run by themselves on macOS yet.", "Steps can't run by themselves on macOS yet")]
    [InlineData("Waiting for a free session slot (`3` of `3` running). This Step starts by itself.", "Waiting for a free session slot")]
    public void The_first_clause_stops_at_the_first_comma_full_stop_colon_or_bracket(string sentence, string clause) =>
        Assert.Equal(clause, SandboxParkCardViewModel.FirstClause(sentence));

    [Fact]
    public void The_card_carries_the_whole_sentence_its_command_and_a_severity_word()
    {
        var card = new SandboxParkCardViewModel(Blocker("This machine has no sandbox tool, so this Step can't run by itself."), "x", _bed.Check);

        Assert.Equal(("This machine has no sandbox tool, so this Step can't run by itself.", "sudo apt install bubblewrap", Labels.SandboxSeverityBlocked, StateGlyph.Down), (card.Sentence, card.Row.Command, card.Severity, card.Glyph));
    }

    [Theory]
    [InlineData("gray", "Not yet supported")]
    [InlineData("wait", "Waiting")]
    public void A_gray_or_waiting_card_is_never_red(string state, string word)
    {
        var card = new SandboxParkCardViewModel(Blocker("Steps can't run by themselves on macOS yet.", state), "x", _bed.Check);

        Assert.Equal((word, StateGlyph.Stale), (card.Severity, card.Glyph));
    }
}
