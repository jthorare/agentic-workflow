using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Item;
using Aw.Sindri.Ui.ViewModels.Run;
using Aw.Sindri.Ui.ViewModels.Takeover;
using Aw.Sindri.Ui.Views.Agents;
using Aw.Sindri.Ui.Views.Components;
using Aw.Sindri.Ui.Views.Instance;
using Aw.Sindri.Ui.Views.Item;
using Aw.Sindri.Ui.Views.Run;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The sandbox rows of the accessibility table (sandbox spec §8, accessibility.json), each in a real headless window: the Sandbox line, Blocked list, Allow, Waiting, Limit ending and Takeover banner.</summary>
[Lane(TestLane.Integration)]
public sealed class SandboxSheetViewTests : FrameHost
{
    private const string Stepped = "claude-code/aaaaaaaa-0000-4000-8000-000000000101";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static Window Show(Control content, double width = 900, double height = 700)
    {
        var window = new Window { Width = width, Height = height, Content = content };
        window.Show();
        Settle(window);
        return window;
    }

    private static bool Polite(Control live) => AutomationProperties.GetLiveSetting(live) == AutomationLiveSetting.Polite;

    private async Task<Window> ItemSheetShown(Action<ItemSheetViewModel>? before = null)
    {
        var catalog = new CatalogViewModel(new FakeClipboard(), new FakeTimeProvider(A.Noon));
        catalog.Show(_api, Server, (await Server.Commands(_api, Token)).Value);
        var sheet = new ItemSheetViewModel(Server, catalog, new FakeTimeProvider(A.Noon));
        await sheet.Open(_api, A.NewItem, Token);
        before?.Invoke(sheet);
        return Show(new ItemSheet { DataContext = sheet });
    }

    [Fact]
    public Task The_sandbox_line_and_headroom_are_text_the_screen_reader_reads_and_the_line_dims_when_serve_is_down() => OnUi(async () =>
    {
        Server.SessionOneBody = (_, id) =>
        {
            var row = JsonNode.Parse(GoldenFiles.SpecShaped("sessions", "sandbox-rows"))!["sessions"]!.AsArray().Single(r => string.Equals(r!["id"]!.GetValue<string>(), id, StringComparison.Ordinal))!.DeepClone();
            return new RouteAnswer(HttpStatusCode.OK, new JsonObject { ["session"] = row, ["cwd"] = "/w", ["stateHistory"] = new JsonArray(), ["asOf"] = "2026-10-28T09:00:00.0000000Z" }.ToJsonString());
        };
        var sheet = new SessionSheetViewModel(Server);
        await sheet.Open(_api, Stepped, Token);
        var window = Show(new SessionSheetView { DataContext = sheet });

        Assert.Equal("Sandbox: on, writes only to /home/thor/repos/aw-wt/api-sb6", Heard.Wrapper(Named<Border>(window, "SessionSandbox")));
        Assert.Equal("memory 2.1 of 12 GiB", Heard.Wrapper(Named<Border>(window, "SessionSandboxHeadroom")));
        Assert.Equal(AutomationControlType.Text, ControlAutomationPeer.CreatePeerForElement(Named<Border>(window, "SessionSandbox")).GetAutomationControlType());
        Assert.DoesNotContain("dimmed", Named<Border>(window, "SessionSandbox").Classes, StringComparer.Ordinal);

        Server.SessionAnswer = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-001", "down");
        Server.SessionOneBody = null;
        await sheet.Refresh(_api, Token);
        Settle(window);

        Assert.Equal("Sandbox: status unavailable", Heard.Wrapper(Named<Border>(window, "SessionSandbox")));
        Assert.Contains("dimmed", Named<Border>(window, "SessionSandbox").Classes, StringComparer.Ordinal);
        Assert.False(Visible(window, "SessionSandboxHeadroom"));
        window.Close();
    });

    [Fact]
    public Task The_blocked_list_reads_each_row_by_kind_and_only_a_host_or_home_path_has_an_Allow_button() => OnUi(async () =>
    {
        var window = await ItemSheetShown();

        var texts = Named<Control>(window, "ItemBlockedList").GetVisualDescendants().OfType<Border>().Where(b => b.Name == "ItemBlockedText").Select(Heard.Name).ToList();
        Assert.Equal(
            [
                "Blocked host registry.example.org 3 times; allow it for this repo",
                "Blocked path /home/thor/.config/tool 1 times; allow it for this repo",
                "Blocked path /home/thor/.ssh: never allowed, Keys and tokens are never shared with a session.",
                "Blocked tool git push 4 times; push from your own terminal",
            ],
            texts,
            StringComparer.Ordinal);
        var buttons = Named<Control>(window, "ItemBlockedList").GetVisualDescendants().OfType<Button>().Where(b => b.Name == "ItemBlockedAllow" && b.IsEffectivelyVisible).ToList();
        Assert.Equal(["Allow registry.example.org", "Allow /home/thor/.config/tool"], buttons.Select(Heard.Name), StringComparer.Ordinal);
        Assert.All(buttons, b => Assert.True(b.Focusable && b.IsEnabled));
        window.Close();
    });

    [Fact]
    public Task Allow_keeps_focus_through_every_state_and_one_polite_zero_width_region_says_each_step() => OnUi(async () =>
    {
        var window = await ItemSheetShown();
        var live = Named<TextBlock>(window, "ItemBlockedAnnouncement");
        var allow = Named<Control>(window, "ItemBlockedList").GetVisualDescendants().OfType<Button>().First(b => b.Name == "ItemBlockedAllow");
        Assert.Equal((0d, 0d), (live.Width, live.Opacity));
        _ = allow.Focus();

        allow.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Settle(window);

        Assert.Same(allow, window.FocusManager!.GetFocusedElement());
        Assert.Equal("Needs your approval: open the approval to allow registry.example.org", live.Text);
        Assert.True(Polite(live));

        // The owner approved the edit: nothing is left to approve and the approved hash moved on.
        Server.ProfileAnswer = new RouteAnswer(HttpStatusCode.OK, GoldenFiles.Api("profile", "approved").Replace("70e1581b3ecaf617318482f6b0844b67f79a287a7136b4e4b6ff5d26005e4261", "cc73e26e84c70b5c151d3b0ea90bb7f048a85174433c1b8620f8ba68d8479ee2", StringComparison.Ordinal));
        await ((ItemSheetViewModel)((ItemSheet)window.Content!).DataContext!).Blocked.Read(_api, A.NewItem, Token);
        Settle(window);

        Assert.Same(allow, window.FocusManager.GetFocusedElement());
        Assert.Equal("Allowed", live.Text);
        window.Close();
    });

    [Fact]
    public Task Waiting_for_a_slot_is_a_word_in_the_runs_row_said_once_never_takes_focus_and_ends_with_Started() => OnUi(async () =>
    {
        var status = FixtureInstances.Parse<StatusFields>(JsonNode.Parse(GoldenFiles.SpecShaped("workflow-status", "sandbox-waiting-for-slot"))!);
        var expanded = new ExpandedStateViewModel(new CurrentState(status.Header.State, Step: null, Gates: [], Desk: null, status), status.Header.Address, new FakeClipboard(), new FakeTimeProvider(A.Noon));
        var window = Show(new ExpandedStateView { DataContext = expanded });
        await Until(window, () => expanded.WaitingVoice.IsAnnouncing, "the wait was never said");
        var live = Named<TextBlock>(window, "ExpandedWaitingAnnouncement");

        Assert.Equal("Waiting for a free session slot (3 of 3 running)", Heard.Name(Named<Border>(window, "ExpandedWaiting")));
        Assert.Equal(("Waiting for a free session slot, 3 of 3 running", 0d, true), (live.Text, live.Width, Polite(live)));
        Assert.Null(window.FocusManager!.GetFocusedElement());

        expanded.Update(new CurrentState(status.Header.State, Step: null, Gates: [], Desk: null, status with { Run = status.Run! with { WaitingForSlot = null, Session = new SessionHandle("claude", "abc") } }));
        Settle(window);

        Assert.Equal(("Started", "Started"), (live.Text, Named<TextBlock>(window, "ExpandedWaitingLine").Text));
        Assert.Null(window.FocusManager.GetFocusedElement());
        window.Close();
    });

    [Fact]
    public Task A_limit_ending_is_one_sentence_in_the_failure_row_and_that_sentence_is_its_name() => OnUi(async () =>
    {
        var line = RunFixtures.Line("plan", new Dictionary<string, string>(StringComparer.Ordinal) { ["plan"] = "FAILED" }) with { State = "stuck", Since = A.Noon.AddHours(-1) };
        line = line with { States = [.. line.States.Select(c => string.Equals(c.State, "stuck", StringComparison.Ordinal) ? c with { Glyph = StateGlyph.Done, Outcome = "failed" } : c)] };
        var child = RunFixtures.Child("plan", ("produce", StateGlyph.Active, false), ("check", StateGlyph.Pending, false)) with
        {
            StepRuns = [new StepRun("review.1", "produce", Attempt: 1, MaxAttempts: null, "failed", "agent", A.Noon.AddHours(-4), A.Noon.AddHours(-3), DurationMs: 3_600_000, Outcome: null, Tokens: null, Actor: null)],
        };
        static RouteAnswer Body<T>(T value) => new(HttpStatusCode.OK, JsonSerializer.Serialize(value, SindriApiClient.Json));
        Server.InstanceBody = id => string.Equals(id, line.Id, StringComparison.Ordinal) ? Body(line)
            : string.Equals(id, "WFI-plan", StringComparison.Ordinal) ? Body(child)
            : RunFixtures.Stages.Contains(id["WFI-".Length..], StringComparer.Ordinal) ? Body(RunFixtures.Child(id["WFI-".Length..], ("produce", StateGlyph.Done, false))) : null;
        Server.InstanceEventAnswers["WFI-plan"] = RouteAnswer.SpecShaped("workflow-instance-events", "sandbox-limit-memory-branch");
        var catalog = new CatalogViewModel(new FakeClipboard(), new FakeTimeProvider(A.Noon));
        var page = new RunPageViewModel(Server, catalog, new FakeClipboard(), new FakeTimeProvider(A.Noon));
        await page.Open(_api, line.Address, line.Id, Token);
        var window = Show(new RunPage { DataContext = page }, 1200, 800);

        const string Sentence = "This Step ran out of memory (12GiB) after 21 min. Its work so far is kept on branch sindri/itm-0f-review-1.";
        Assert.Equal((Sentence, Sentence), (Named<TextBlock>(window, "RunFailureText").Text, Heard.Name(Named<TextBlock>(window, "RunFailureText"))));
        window.Close();
    });

    [Fact]
    public Task The_takeover_banner_is_read_once_never_takes_focus_dismisses_once_and_comes_back_on_the_next_takeover() => OnUi(() =>
    {
        var banner = new TakeoverBannerViewModel();
        var window = Show(new TakeoverBannerView { DataContext = banner });
        var live = Named<TextBlock>(window, "TakeoverBannerAnnouncement");
        Assert.False(Visible(window, "TakeoverBanner"));

        banner.Update(sandboxed: true, takenOver: true);
        Settle(window);

        Assert.True(Visible(window, "TakeoverBanner"));
        Assert.Equal((GoldenFiles.Sandbox("takeover-banner.txt").Trim(), 0d, true), (live.Text, live.Width, Polite(live)));
        Assert.Equal(banner.Text, Heard.Name(Named<Control>(window, "TakeoverBanner")));
        Assert.Null(window.FocusManager.GetFocusedElement());

        Named<Button>(window, "TakeoverBannerDismiss").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        banner.Update(sandboxed: true, takenOver: true);
        Settle(window);
        Assert.False(Visible(window, "TakeoverBanner"));

        banner.Update(sandboxed: true, takenOver: false);
        banner.Update(sandboxed: true, takenOver: true);
        Settle(window);
        Assert.True(Visible(window, "TakeoverBanner"));
        window.Close();
        return Task.CompletedTask;
    });
}
