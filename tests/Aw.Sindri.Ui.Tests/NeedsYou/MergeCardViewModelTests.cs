using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The merge stop's pull request card (needs-you spec, stop 3): each part in words, and what has not been read yet says so.</summary>
[Lane(TestLane.Unit)]
public sealed class MergeCardViewModelTests
{
    private static FactView Golden(Action<JsonObject>? change = null)
    {
        var fact = JsonNode.Parse(GoldenFiles.Api("workflow-instance-fact", "pr-status"))!;
        change?.Invoke(fact["body"]!.AsObject());
        return new FactView("fact:prStatus", FactType.Payload, System.Text.Json.JsonSerializer.SerializeToElement(fact["body"]), Truncated: false);
    }

    private static MergeCardViewModel Card(Action<JsonObject>? change = null) => new(PrStatusReader.Of(Golden(change)).Value);

    private static string Line(MergeCardViewModel card, string name) => card.Lines.Single(l => string.Equals(l.Name, name, StringComparison.Ordinal)).Value;

    [Fact]
    public void The_golden_reads_as_the_link_and_four_lines()
    {
        var card = Card();

        Assert.Equal(("Pull request", "#12", "Open pull request #12", true), (card.Heading, card.PrText, card.PrName, card.CanOpen));
        Assert.Equal(["Checks", "Review", "Proof", "Docs"], card.Lines.Select(l => l.Name), StringComparer.Ordinal);
        Assert.Equal(("5 passed, 2 failed, 1 pending", "1 open, 2 resolved"), (Line(card, "Checks"), Line(card, "Review")));
    }

    [Fact]
    public void Checks_not_read_yet_say_so()
    {
        var card = Card(body => body["checks"] = null);

        Assert.Equal("Checks not read yet", Line(card, "Checks"));
    }

    [Theory]
    [InlineData(null, "Proof not read yet")]
    [InlineData("passed", "Passed")]
    [InlineData("failed", "Failed")]
    [InlineData("none", "No proof ran")]
    public void Proof_says_what_it_is_or_that_it_was_not_read(string? status, string words)
    {
        var card = Card(body => body["proof"] = status is null ? null : new JsonObject { ["status"] = status });

        Assert.Equal(words, Line(card, "Proof"));
    }

    [Theory]
    [InlineData("updated", "Updated")]
    [InlineData("notNeeded", "Not needed")]
    [InlineData("missing", "Missing")]
    public void Docs_say_which_of_three_states_they_are_in(string status, string words)
    {
        var card = Card(body => body["docs"] = new JsonObject { ["status"] = status });

        Assert.Equal(words, Line(card, "Docs"));
    }

    [Fact]
    public async Task Open_hands_an_http_link_to_the_launcher_and_refuses_any_other_scheme()
    {
        var launcher = new FakeLauncher();
        var web = new MergeCardViewModel(PrStatusReader.Of(Golden()).Value, launcher);
        var odd = new MergeCardViewModel(PrStatusReader.Of(Golden(body => body["url"] = "file:///etc/passwd")).Value, launcher);

        await web.Open(TestContext.Current.CancellationToken);
        await odd.Open(TestContext.Current.CancellationToken);

        Assert.Equal(["https://forge.test/acme/app/pull/12"], launcher.Opened, StringComparer.Ordinal);
        Assert.False(odd.CanOpen);
    }
}
