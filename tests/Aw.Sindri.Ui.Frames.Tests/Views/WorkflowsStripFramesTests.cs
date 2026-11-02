using System.Net;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// U3' key frames (UI spec §18.2: one per state, integration lane) for Home's Workflows strip, against the in-memory API serving
/// the root goldens: the fixture, the incident first, empty, stalled, not served, the protected popover, Home at five seconds and
/// with the large profile. Saved under frames/ as u3-u3p-*.png.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class WorkflowsStripFramesTests : FrameHost
{
    private static List<Border> Frames(Window window) =>
        [.. window.GetVisualDescendants().OfType<Border>().Where(b => b.Name == "StripRowFrame" && b.IsEffectivelyVisible)];

    private static string Address(Border frame) => ((StripRowViewModel)frame.DataContext!).Address;

    // The root golden as served, narrowed to the rows that pass: counts and the empty sentence stay the API's.
    private static RouteAnswer Only(Func<JsonNode, bool> keep)
    {
        var body = (JsonObject)JsonNode.Parse(GoldenFiles.Api("workflow-instances", "root"))!; // why: the golden is a JSON object
        body["instances"] = new JsonArray([.. body["instances"]!.AsArray().Where(r => keep(r!)).Select(r => r!.DeepClone())]);
        return new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
    }

    private static bool IsProtected(JsonNode row) => string.Equals((string?)row["answer"]?["class"], "protected", StringComparison.Ordinal);

    [Fact]
    public Task The_fixture_strip_draws_the_open_rows_the_cap_allows_then_N_more() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.Strip.Rows.Count > 0);

        Expect.Sequence(shell.Strip.Rows.Select(r => r.Address), Frames(window).Select(Address));
        Assert.Equal((true, shell.Strip.MoreText), (Visible(window, "WorkflowsMore"), Named<Button>(window, "WorkflowsMore").Content is TextBlock t ? t.Text : null));
        Assert.Equal(shell.Strip.OpenText, Named<TextBlock>(window, "WorkflowsOpen").Text);
        Frame(window, "u3p-strip-fixture");
    });

    [Fact]
    public Task The_incident_draws_first() => OnUi(async () =>
    {
        var (window, _) = await Connected(s => s.Strip.Rows.Count > 0);

        Assert.Equal(FixtureInstances.Rows[0].Address, Address(Frames(window)[0]));
        Frame(window, "u3p-strip-incident-first");
    });

    [Fact]
    public Task An_empty_strip_shows_the_served_sentence() => OnUi(async () =>
    {
        Server.WorkflowAnswer = RouteAnswer.Api("workflow-instances", "empty");
        var (window, _) = await Connected(s => s.Strip.IsEmpty);

        Assert.Equal((true, (string)GoldenFiles.Node("workflow-instances", "empty")["empty"]!), (Visible(window, "WorkflowsEmpty"), Named<TextBlock>(window, "WorkflowsEmpty").Text));
        Assert.Empty(Frames(window));
        Assert.False(Visible(window, "WorkflowsMore"));
        Frame(window, "u3p-strip-empty");
    });

    [Fact]
    public Task A_stalled_strip_keeps_its_rows_and_says_so() => OnUi(async () =>
    {
        Server.ServiceView = RouteAnswer.SpecShaped("services", "stalled");
        var (window, shell) = await Connected(s => s.Health.Banner.IsStalled && s.Strip.Rows.Count > 0);

        Assert.True(Visible(window, "WorkflowsStale"));
        Assert.NotEmpty(Frames(window));
        Assert.All(shell.Strip.Rows, r => Assert.True(r.IsStale));
        Frame(window, "u3p-strip-stalled");
    });

    [Fact]
    public Task A_strip_before_the_list_route_says_it_arrives_with_API_P11() => OnUi(async () =>
    {
        Server.WorkflowAnswer = RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-API-404", "no such route");
        var (window, shell) = await Connected(s => s.Strip.IsNotServed);

        Assert.Equal((true, shell.Strip.NotServedText), (Visible(window, "WorkflowsNotServed"), Named<TextBlock>(window, "WorkflowsNotServed").Text));
        Assert.True(Visible(window, "NeedsYouPanel") && Visible(window, "RunningPanel"));
        Frame(window, "u3p-strip-not-served");
    });

    // Proof B6: a list in the older shape (main's, before P11) is a sentence on the strip, never an empty box with only its header.
    [Fact]
    public Task A_list_this_build_cant_read_shows_a_sentence_not_an_empty_box() => OnUi(async () =>
    {
        Server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, """{"instances":[],"needsYou":[],"empty":"nothing","rows":[]}""");
        var (window, shell) = await Connected(s => s.Strip.IsUnreadable);

        Assert.Equal((true, ViewModels.Labels.StripUnreadable), (Visible(window, "WorkflowsUnreadable"), Named<TextBlock>(window, "WorkflowsUnreadable").Text));
        Assert.False(Visible(window, "WorkflowsEmpty") || Visible(window, "WorkflowsNotServed") || Visible(window, "WorkflowsMore"));
        Assert.Empty(shell.Strip.Rows);
    });

    [Fact]
    public Task A_protected_row_shows_the_lock_button_and_its_popover_holds_the_served_cli() => OnUi(async () =>
    {
        Server.WorkflowAnswer = Only(IsProtected);
        var (window, shell) = await Connected(s => s.Strip.Rows.Count > 0);
        var row = Assert.Single(shell.Strip.Rows, r => r.Protected is not null);

        var frame = Frames(window).Single(f => string.Equals(Address(f), row.Address, StringComparison.Ordinal));
        Assert.DoesNotContain(frame.GetVisualDescendants().OfType<Button>(), b => b.Name == "StripAnswer" && b.IsEffectivelyVisible);
        var lockButton = Named<Button>(frame, "LockButton");
        _ = lockButton.Focus(NavigationMethod.Tab);
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, keySymbol: null);
        Settle(window);

        Assert.True(lockButton.Flyout!.IsOpen);
        var popover = (Visual)((Flyout)lockButton.Flyout).Content!;
        Assert.Equal(row.Protected!.Cli, Named<TextBlock>(popover, "ServedCli").Text);
        Frame(window, "u3p-strip-protected-popover");
    });

    [Fact]
    public Task Every_strip_control_is_named_label_plus_address() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.Strip.Rows.Count > 0);

        foreach (var frame in Frames(window))
        {
            var row = (StripRowViewModel)frame.DataContext!;
            Assert.Equal(row.AccessibleName, AutomationProperties.GetName(frame));
            Assert.DoesNotContain(row.Address, AutomationProperties.GetName(frame), StringComparison.Ordinal);   // the title says which run; the address lives in Details
            Assert.Equal((AccessibilityView.Control, "ListItem"), (AutomationProperties.GetAccessibilityView(frame), AutomationProperties.GetControlTypeOverride(frame)?.ToString()));
            foreach (var button in frame.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible))
            {
                Assert.DoesNotContain(row.Address, AutomationProperties.GetName(button), StringComparison.Ordinal);
            }
        }

        Assert.EndsWith(shell.Strip.Heading, AutomationProperties.GetName(Named<Border>(window, "WorkflowsPanel")), StringComparison.Ordinal);
    });

    [Fact]
    public Task Strip_rows_head_the_right_column_beside_needs_you_and_above_running() => OnUi(async () =>
    {
        var (window, _) = await Connected(s => s.Strip.Rows.Count > 0 && s.NeedsYou.Count > 0);

        double Top(string name) => Named<Control>(window, name).TranslatePoint(new Point(0, 0), window)!.Value.Y; // why: the panels are in the tree
        double Left(string name) => Named<Control>(window, name).TranslatePoint(new Point(0, 0), window)!.Value.X; // why: the panels are in the tree
        Assert.True(Left("WorkflowsPanel") > Left("NeedsYouPanel") && Top("WorkflowsPanel") < Top("RunningPanel")); // why: F3 home spec: the strip heads the right column, Running under it
    });

    [Fact]
    public Task Home_at_five_seconds_has_the_strip_beside_needs_you() => OnUi(async () =>
    {
        Server.NeedsYouView = RouteAnswer.Api("needs-you", "fixture");
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0 && s.Strip.Rows.Count > 0);

        Assert.Equal(Math.Min(StripCap(shell.NeedsYou.Rows.Count), FixtureInstances.Rows.Count(FixtureInstances.IsOpen)), Frames(window).Count);
        Frame(window, "u3p-home-five-second-strip");
    });

    [Fact]
    public Task Home_with_the_large_profile_gives_the_strip_what_needs_you_leaves() => OnUi(async () =>
    {
        Server.NeedsYouView = RouteAnswer.Api("needs-you", "large");
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 5 && s.Strip.Rows.Count > 0, width: 1400);

        Assert.Equal(StripCap(shell.NeedsYou.Rows.Count), Frames(window).Count);
        Assert.True(Visible(window, "RunningPanel") && Visible(window, "QueuedPanel"));
        Frame(window, "u3p-home-large-strip");
    });

    // The fold rule at text scale 1.0 (screens §4.1): what is left of eight rows after the Needs-you rows shown, at most five.
    private static int StripCap(int needsYouShown) => Math.Clamp(8 - needsYouShown, 0, WorkflowsStripViewModel.Cap);
}
