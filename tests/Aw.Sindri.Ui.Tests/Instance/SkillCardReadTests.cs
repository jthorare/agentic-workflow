using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Facts;
using Aw.Sindri.Ui.ViewModels.Instance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>The instance frame's skill card across re-reads (skill flows spec §5.2): a failed needs-you read keeps the last rows, and a failed skills read still draws the card and is retried.</summary>
[Lane(TestLane.Unit)]
public sealed class SkillCardReadTests
{
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

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
                ["text"] = "Which repo?",
                ["resultPath"] = "/r.json",
                ["detail"] = null,
                ["instance"] = address,
                ["state"] = "mitigate",
                ["attempt"] = 1,
            },
        };

    private async Task<InstanceFrameViewModel> Opened()
    {
        var detail = FixtureInstances.Detail("incident-loop");
        var body = GoldenFiles.Node("workflow-instance", "incident-loop");
        body["states"]!.AsArray().Single(s => (string?)s!["id"] == "mitigate")!["block"] = "step.skill.office-hours";
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
        var rows = GoldenFiles.Node("needs-you", "desk-task");
        rows["rows"]!.AsArray().Add(WaitingRow(detail.Address));
        _server.NeedsYouView = new RouteAnswer(HttpStatusCode.OK, rows.ToJsonString());
        var frame = new InstanceFrameViewModel(_server, new FakeClipboard(), _clock);
        await frame.Open(FactFixtures.Api, detail.Address, detail.Id, Token);
        return frame;
    }

    [Fact]
    public async Task A_failed_needs_you_read_keeps_the_waiting_card()
    {
        var frame = await Opened();
        var before = frame.Expanded!.SkillCard!.State;

        _server.NeedsYouView = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-API-500", "boom");
        await frame.Refresh(FactFixtures.Api, Token);

        Assert.Equal((SkillCardState.WaitingOnYou, SkillCardState.WaitingOnYou), (before, frame.Expanded!.SkillCard!.State));
    }

    [Fact]
    public async Task A_failed_skills_read_still_draws_the_card_and_the_next_read_fills_in_the_sentence()
    {
        _server.SkillsAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-API-500", "boom");
        var frame = await Opened();
        var drawn = (frame.Expanded!.HasSkillCard, frame.Expanded.SkillCard?.Sentence);

        _server.SkillsAnswer = RouteAnswer.Api("skills", "fixtures-loaded");
        await frame.Refresh(FactFixtures.Api, Token);

        Assert.Equal((true, string.Empty), drawn);
        Assert.Equal("Shapes an idea into a plan folder with requirements and tasks.", frame.Expanded.SkillCard!.Sentence);
    }
}
