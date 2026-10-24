using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Instance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>
/// Grant tokens in the app (per-round budgets brief, 2026-10-21; q93): the amount starts at the served suggestion and one plain click sends the grant as
/// the one 'POST …/commands' with the amount and no challenge, hold or typed text. A refusal is shown in the server's words.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class GrantTokensTests
{
    private const string Address = "ITM-01JH5/step/1";
    private const string Served = "sindri workflow answer ITM-01JH5/step/1 --grant-tokens 1M";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private GrantTokensViewModel Grant() => new(_server, () => _api, ("WFI-44393da815f188cea9c6fe30ae2a30a6", Address), Served);

    [Fact]
    public void The_amount_starts_at_the_suggestion_the_park_served()
    {
        var grant = Grant();

        Assert.Equal(("1M", 1_000_000), (grant.Amount, grant.AmountTokens));
    }

    [Theory]
    [InlineData("500k", 500_000)]
    [InlineData("2M", 2_000_000)]
    [InlineData("750000", 750_000)]
    public void The_amount_reads_as_recipes_write_it(string text, int tokens)
    {
        var grant = Grant();

        grant.Amount = text;

        Assert.Equal(tokens, grant.AmountTokens);
    }

    [Theory]
    [InlineData("")]
    [InlineData("lots")]
    [InlineData("0")]
    [InlineData("-5k")]
    [InlineData("99999999999M")]
    public async Task An_amount_that_is_not_tokens_says_so_and_cannot_be_granted(string text)
    {
        var grant = Grant();

        grant.Amount = text;
        await grant.Grant();

        Assert.Null(grant.AmountTokens);
        Assert.Equal(text.Trim().Length > 0, grant.ShowsAmountProblem);
        Assert.False(grant.CanGrant);
        Assert.Empty(_server.CommandCalls);
    }

    [Fact]
    public async Task One_click_sends_the_grant_once_with_the_amount_and_no_challenge()
    {
        var grant = Grant();
        var granted = 0;
        grant.Granted += (_, _) => granted++;

        await grant.Grant();
        await grant.Grant();

        var (instance, sent) = _server.CommandCalls.Single();
        Assert.Equal(("WFI-44393da815f188cea9c6fe30ae2a30a6", CommandKind.Answer, AnswerVariant.GrantTokens, 1_000_000), (instance, sent.Kind, sent.Answer, sent.Amount));
        Assert.Equal(Address, sent.Instance);
        Assert.Equal((1, true), (granted, grant.IsDone));
    }

    [Fact]
    public async Task A_refusal_is_shown_in_the_servers_words_and_the_grant_can_be_pressed_again()
    {
        _server.CommandAnswer = RouteAnswer.Problem(HttpStatusCode.UnprocessableEntity, "SND-WF-003", "--grant-tokens 1M is outside 1..400k, 4x the original budget of 100k (fix: use --grant-tokens 1 to --grant-tokens 400k)");
        var grant = Grant();

        await grant.Grant();

        Assert.Contains("outside 1..400k", grant.Error, StringComparison.Ordinal);
        Assert.Equal((false, true), (grant.IsDone, grant.CanGrant));
    }

    [Fact]
    public async Task The_frame_shows_the_control_on_a_budget_park()
    {
        var node = GoldenFiles.Node("workflow-instance", "item-produce");
        node["current"]!["status"]!["answer"] = new JsonObject
        {
            ["kind"] = "grantTokens",
            ["actionId"] = "answer-grant-tokens",
            ["class"] = "protected",
            ["cli"] = "sindri workflow answer ITM-01JE7/item/1/step.4 --grant-tokens 1M",
            ["atTerminal"] = false,
        };
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, node.ToJsonString());
        var frame = new InstanceFrameViewModel(_server, new FakeClipboard(), new FakeTimeProvider());

        await frame.Open(_api, (string)node["address"]!, (string)node["id"]!, Token);

        Assert.Equal("1M", frame.Grant!.Suggested);
    }

    [Fact]
    public async Task The_frame_shows_no_control_without_a_token_grant_answer()
    {
        var node = GoldenFiles.Node("workflow-instance", "item-produce");
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, node.ToJsonString());
        var frame = new InstanceFrameViewModel(_server, new FakeClipboard(), new FakeTimeProvider());

        await frame.Open(_api, (string)node["address"]!, (string)node["id"]!, Token);

        Assert.Null(frame.Grant);
    }
}
