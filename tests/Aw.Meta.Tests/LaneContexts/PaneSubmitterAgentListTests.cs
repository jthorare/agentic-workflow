using Aw.Meta.Core.Coordination;

namespace Aw.Meta.Tests.LaneContexts;

/// <summary>A lead with background agents has an agent list drawn under its input box, and the list's selector row starts with the prompt mark too (#3661).</summary>
public sealed class PaneSubmitterAgentListTests
{
    private const string Line = "Read and continue from /home/x/digests/t1-api-2026-10-17.md";

    // From a real capture (tmux capture-pane -p) of a lead with its agent list open; names scrubbed, rules shortened.
    private static string WithAgentList(string boxText) =>
        string.Join('\n',
        [
            "  ✔ Update installed · /restart to apply",
            "────────────── sindri-v3-tools ─",
            "❯ " + boxText,
            "────────────────────────────────",
            "  Context    │ Model      │ Branch",
            "  █░░░░ 25%  │ Opus 5.5   │ main",
            "  -- INSERT -- ⏵⏵ bypass permissions on",
            "",
            "  ● main",
            "❯ ◯ agent-one",
            "  ◯ agent-two",
            "  ◯ 1 idle agent",
            string.Empty,
        ]);

    [Fact, Trait("Lane", "unit")]
    public void Text_in_the_input_box_is_found_when_an_agent_list_sits_under_it() =>
        Assert.True(PaneSubmitter.InputHolds(WithAgentList(Line), Line));

    [Fact, Trait("Lane", "unit")]
    public void An_empty_input_box_with_an_agent_list_under_it_reads_as_submitted() =>
        Assert.False(PaneSubmitter.InputHolds(WithAgentList(string.Empty), Line));

    [Fact, Trait("Lane", "unit")]
    public void A_pane_with_no_agent_list_still_finds_its_input_box() =>
        Assert.True(PaneSubmitter.InputHolds("────\n❯ " + Line + "\n────\n  Context │ Model\n", Line));
}
