using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The per-verb desk buttons (needs-you spec, stop 3 as amended): one button per recipe verb the instance accepts now, the merge asking first and sent as the served desk action, an unknown verb shown as served.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouVerbsTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    // The desk instance with these recipe verbs among its allowed commands (a null flag leaves 'destructive' unserved), and its complete command protected when asked.
    private void Serve(bool protectedComplete, params (string Verb, bool? Destructive)[] verbs)
    {
        var instance = JsonNode.Parse(GoldenFiles.Api("workflow-instance", "dev-desk-round-trip"))!;
        if (protectedComplete)
        {
            foreach (var command in instance["current"]!["status"]!["commands"]!.AsArray().Where(c => c!["verb"]!.GetValue<string>() == "complete"))
            {
                command!["class"] = "protected";
            }
        }

        var allowed = new JsonArray();
        foreach (var (verb, destructive) in verbs)
        {
            var entry = new JsonObject { ["actionId"] = "ApplyCommand:command", ["cli"] = "sindri workflow command ITM-1/pr-babysitter/1 " + verb, ["verb"] = verb };
            if (destructive is { } flag)
            {
                entry["destructive"] = flag;
            }

            allowed.Add(entry);
        }

        instance["allowedCommands"] = allowed;
        _server.InstanceBody = _ => new RouteAnswer(HttpStatusCode.OK, instance.ToJsonString());
    }

    private async Task<NeedsYouBarViewModel> Bar()
    {
        var bar = new NeedsYouBarViewModel(new DeskReporter(_server, () => _api), new FakeClipboard(), _clock);
        bar.Show(new NeedsYouRowViewModel(NeedsYouRows.Desk(), seen: true, reducedMotion: false, arrived: false, _clock));
        await bar.Loaded;
        return bar;
    }

    private static DeskVerbViewModel Verb(NeedsYouBarViewModel bar, string verb) => bar.Verbs.Single(v => string.Equals(v.Verb, verb, StringComparison.Ordinal));

    [Theory]
    [InlineData("comments", "Held comments")]
    [InlineData("resync-docs", "Resync docs")]
    [InlineData("check", "Check now")]
    [InlineData("rework", "Send back to rework")]
    [InlineData("merge", "Merge")]
    [InlineData("polish", "polish")]
    public async Task Each_verb_has_its_own_words_and_an_unknown_one_shows_as_served(string verb, string words)
    {
        Serve(protectedComplete: false, (verb, null));

        var bar = await Bar();

        Assert.Equal((true, words), (bar.HasVerbs, Verb(bar, verb).Label));
        Assert.StartsWith(words, bar.Verbs[0].Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_buttons_come_in_the_order_the_instance_serves_them_once_each()
    {
        Serve(protectedComplete: false, ("merge", null), ("rework", null), ("comments", null), ("check", null), ("comments", null));

        var bar = await Bar();

        Assert.Equal(["merge", "rework", "comments", "check"], bar.Verbs.Select(v => v.Verb), StringComparer.Ordinal);
    }

    [Fact]
    public async Task An_instance_that_serves_no_verb_has_no_buttons()
    {
        Serve(protectedComplete: false);

        var bar = await Bar();

        Assert.Equal((false, false), (bar.HasVerbs, bar.HasMergeVerb));
    }

    [Fact]
    public async Task A_verb_that_does_not_ask_is_sent_at_once_as_the_served_command()
    {
        Serve(protectedComplete: false, ("check", null));
        var bar = await Bar();

        await Verb(bar, "check").Press();

        var (instance, body) = Assert.Single(_server.CommandCalls);
        Assert.Equal((CommandKind.Command, "check"), (body.Kind, body.Verb));
        Assert.Equal(instance, body.Instance);
        Assert.Equal("Check now: sent", bar.VerbNote);
    }

    [Fact]
    public async Task The_merge_asks_one_line_first_and_Keep_sends_nothing()
    {
        Serve(protectedComplete: true, ("merge", true), ("check", null));
        var bar = await Bar();
        var merge = Verb(bar, "merge");

        await merge.Press();
        Assert.Equal((true, false, "Merge this pull request? It cannot be undone."), (merge.IsConfirming, merge.ShowsButton, merge.Question));
        Assert.Empty(_server.CommandCalls);

        merge.Keep();
        Assert.Equal((false, true), (merge.IsConfirming, merge.ShowsButton));
        Assert.Empty(_server.CommandCalls);
    }

    [Fact]
    public async Task Confirming_the_merge_sends_it_once_and_the_bar_says_it_is_done()
    {
        Serve(protectedComplete: true, ("merge", null));
        var bar = await Bar();
        var merge = Verb(bar, "merge");
        await merge.Press();

        await merge.Confirm();
        await merge.Confirm();

        var (_, body) = Assert.Single(_server.CommandCalls);
        Assert.Equal((CommandKind.Command, "merge"), (body.Kind, body.Verb));
        Assert.True(bar.IsApproved);
    }

    [Fact]
    public async Task The_merge_is_a_button_and_not_a_terminal_copy_but_a_protected_step_without_the_verb_still_is()
    {
        Serve(protectedComplete: true, ("merge", null));
        var withVerb = await Bar();
        Serve(protectedComplete: true);
        var without = await Bar();

        Assert.Equal((false, true), (withVerb.ShowsTerminal, without.ShowsTerminal));
        Assert.True(withVerb.HasBar);
    }

    [Theory]
    [InlineData("merge", false, false)]
    [InlineData("check", true, true)]
    [InlineData("merge", null, false)]
    [InlineData("check", null, false)]
    public async Task Only_the_served_destructive_flag_decides_whether_a_verb_asks(string verb, bool? served, bool asks)
    {
        Serve(protectedComplete: false, (verb, served));

        var bar = await Bar();

        Assert.Equal(asks, Verb(bar, verb).IsDestructive);
    }

    [Fact]
    public async Task A_refusal_shows_as_served_and_leaves_the_verbs_pressable()
    {
        Serve(protectedComplete: false, ("rework", null));
        _server.CommandAnswer = RouteAnswer.Problem(HttpStatusCode.Forbidden, "SND-AUTH-002", "An agent session can't do this.");
        var bar = await Bar();

        await Verb(bar, "rework").Press();

        Assert.Equal(("An agent session can't do this.", true, string.Empty), (bar.Refusal, bar.VerbsEnabled, bar.VerbNote));
    }
}
