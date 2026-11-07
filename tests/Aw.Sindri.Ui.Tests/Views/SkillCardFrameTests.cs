using System.Net;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>A skill block's state drawn on the instance sheet (skill flows spec §5.2): the card from the instance, the skills list and the needs-you row, in the real shell.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class SkillCardFrameTests : FrameHost
{
    private const string Question = "Which repo should this plan cover?";

    private static JsonObject WaitingRow(string address) =>
        new()
        {
            ["id"] = "skill:" + address + ":mitigate",
            ["kind"] = "skillWaiting",
            ["label"] = "skill",
            ["subject"] = null,
            ["item"] = null,
            ["title"] = "Mitigate",
            ["reason"] = "waiting",
            ["repo"] = null,
            ["urgency"] = "calm",
            ["since"] = "2026-01-01T00:00:00.0000000Z",
            ["recordedOnly"] = false,
            ["notice"] = null,
            ["answerKind"] = null,
            ["primaryAction"] = null,
            ["actions"] = new JsonArray(),
            ["cli"] = "sindri workflow status",
            ["ownerWaiting"] = new JsonObject
            {
                ["launchId"] = "LCH-1",
                ["reason"] = "question",
                ["text"] = Question,
                ["resultPath"] = "/r.json",
                ["detail"] = null,
                ["instance"] = address,
                ["state"] = "mitigate",
                ["attempt"] = 1,
            },
        };

    // incident-loop's current state ('mitigate') runs the office-hours block, and the server lists a skillWaiting row for it.
    private void Serve(bool waiting)
    {
        var detail = FixtureInstances.Detail("incident-loop");
        var body = GoldenFiles.Node("workflow-instance", "incident-loop");
        var cell = body["states"]!.AsArray().Single(s => (string?)s!["id"] == "mitigate")!;
        cell["block"] = "step.skill.office-hours";
        Server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
        if (!waiting)
        {
            return;
        }

        var rows = GoldenFiles.Node("needs-you", "desk-task");
        rows["rows"]!.AsArray().Add(WaitingRow(detail.Address));
        Server.NeedsYouView = new RouteAnswer(HttpStatusCode.OK, rows.ToJsonString());
    }

    [Fact]
    public Task The_card_shows_the_blocks_sentence_a_state_word_and_the_question_the_needs_you_row_carries() => OnUi(async () =>
    {
        var detail = FixtureInstances.Detail("incident-loop");
        var (window, shell) = await Connected();
        Serve(waiting: true);

        shell.OpenInstance(detail.Address, detail.Id);
        await Until(window, () => shell.Instance.Phase == FramePhase.Shown && shell.Instance.Expanded?.HasSkillCard == true);

        Assert.Equal("Shapes an idea into a plan folder with requirements and tasks.", Named<TextBlock>(window, "SkillCardSentence").Text);
        Assert.Equal("waiting on you", Named<TextBlock>(window, "SkillCardState").Text);
        Assert.Equal("Mitigate is waiting on you: " + Question, Named<TextBlock>(window, "SkillCardHeadline").Text);
        Assert.False(Visible(window, "SkillCardTakeOver"));   // the sheet has the one Take over
    });

    [Fact]
    public Task The_waiting_text_is_in_a_zero_width_polite_live_region_and_a_quiet_card_has_no_question() => OnUi(async () =>
    {
        var detail = FixtureInstances.Detail("incident-loop");
        var (window, shell) = await Connected();
        Serve(waiting: true);

        shell.OpenInstance(detail.Address, detail.Id);
        await Until(window, () => shell.Instance.Expanded?.HasSkillCard == true);
        var region = Named<TextBlock>(window, "SkillCardAnnouncement");
        await Until(window, () => region.Text != "Skill status", "the waiting text was never said after the card attached");

        Assert.Equal((0d, "Mitigate is waiting on you: " + Question), (region.Width, region.Text));
    });

    [Fact]
    public Task A_state_that_runs_no_skill_block_draws_no_card() => OnUi(async () =>
    {
        var detail = FixtureInstances.Detail("incident-loop");
        var (window, shell) = await Connected();

        shell.OpenInstance(detail.Address, detail.Id);
        await Until(window, () => shell.Instance.Phase == FramePhase.Shown);

        Assert.False(Visible(window, "SkillCardSentence"));
    });
}
