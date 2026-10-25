using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The desk bar next to the answer (engine q65): a button for each review, the served agent session and the reason code in words.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouDeskReviewTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new();
    private readonly FakeLauncher _launcher = new();

    private async Task<NeedsYouBarViewModel> Bar(Action<JsonNode> edit)
    {
        var node = JsonNode.Parse(GoldenFiles.Api("workflow-instance", "dev-desk-round-trip"))!;
        edit(node);
        _server.InstanceBody = _ => new RouteAnswer(HttpStatusCode.OK, node.ToJsonString());
        var bar = new NeedsYouBarViewModel(new DeskReporter(_server, () => _api, _launcher), _clock);
        bar.Show(new NeedsYouRowViewModel(NeedsYouRows.Desk(), seen: true, reducedMotion: false, arrived: false, _clock));
        await bar.Loaded;
        return bar;
    }

    [Fact]
    public async Task A_desk_with_reviews_offers_a_button_each_and_a_link_opens()
    {
        var bar = await Bar(n => n["current"]!["desk"]!["reviews"] = JsonNode.Parse("""[{"fact":"fact:pr","kind":"pr","url":"https://example.test/pull/7"},{"fact":"fact:design","kind":"design"}]"""));

        Assert.True(bar.HasReviews);
        Assert.Equal(["Open the PR", "Open the design"], bar.Reviews.Select(r => r.Label), StringComparer.Ordinal);
        await bar.Reviews[0].Open();
        Assert.Equal(["https://example.test/pull/7"], _launcher.Opened, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_review_with_nothing_to_open_takes_the_person_to_the_instances_frame()
    {
        var bar = await Bar(n => n["current"]!["desk"]!["reviews"] = JsonNode.Parse("""[{"fact":"fact:design","kind":"design"}]"""));
        var asked = new List<NeedsYouFactEventArgs>();
        bar.FactRequested += (_, e) => asked.Add(e);

        await bar.Reviews[0].Open();

        var one = Assert.Single(asked);
        Assert.Equal(("fact:design", GoldenFiles.Node("workflow-instance", "dev-desk-round-trip")["address"]!.GetValue<string>()), (one.Fact, one.Address));
    }

    [Fact]
    public async Task A_named_session_shows_a_link_that_opens_its_sheet()
    {
        var bar = await Bar(n => n["current"]!["status"]!["run"] = JsonNode.Parse("""{"runtime":"agent","launch":"LCH-1","session":{"provider":"claude","id":"abc123"}}"""));
        var opened = new List<string>();
        bar.SessionRequested += (_, e) => opened.Add(e.SessionRef);

        bar.OpenSession();

        Assert.True(bar.HasSession);
        Assert.Equal(["claude/abc123"], opened, StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_reason_code_reads_in_words_and_a_desk_without_any_of_it_is_unchanged()
    {
        var withCode = await Bar(n => n["current"]!["status"]!["reasonCode"] = "SND-WF-010");
        var plain = await Bar(_ => { });

        Assert.Equal(("The step's dispatch was refused", false, false, false), (withCode.ReasonLine, plain.HasReviews, plain.HasSession, plain.HasReasonLine));
    }
}
