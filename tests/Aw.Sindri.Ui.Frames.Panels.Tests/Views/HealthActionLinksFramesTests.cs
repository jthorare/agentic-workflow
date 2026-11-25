using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Sessions;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Panels.Tests.Views;

/// <summary>
/// The System page's in-card actions read as links (#4317): Set up, Open Agents and View job carry the accent-link class, rest in Brush.Primary (the colour Connect
/// Linear uses) and turn Brush.PrimaryHover under the pointer. Before the fix they were row-title only and drew in plain body ink. Each frame is saved before the check.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class HealthActionLinksFramesTests : FrameHost
{
    private static Color TokenColor(string key) =>
        Application.Current is { } app && app.TryFindResource(key, app.ActualThemeVariant, out var value) && value is ISolidColorBrush brush ? brush.Color : throw new InvalidOperationException($"{key} is not a brush token");

    private static Color? BrushColor(IBrush? brush) => (brush as ISolidColorBrush)?.Color;

    // The System page with a missing profile, one agents problem and a service with a run in flight: every in-card action is on screen.
    private async Task<(Window Window, ShellViewModel Shell)> Shown()
    {
        var rows = new[] { new SessionProblemView("claude-records", "SND-SESSION-001", "The claude-records source couldn't be read in the last pass") };
        var missing = A.Status(profile: new ProfileView(Live: null, Problem: "No profile found", Source: null, ProblemKind: "missing"));
        Server.Current = missing with { Sessions = new SessionCountsView(Live: 3, Asking: 1, Problems: 1, Running: 1, ProblemRows: rows), };
        Server.ServiceView = RouteAnswer.Api("services", "running-job");
        var (window, shell) = await Connected(width: 1600, height: 1000);
        shell.GoTo(PageId.System);
        await Until(window, () => Visible(window, "ProfileSetupLink") && Visible(window, "AgentsLink"), "the System page never drew Set up and Open Agents");
        return (window, shell);
    }

    // The page's visible row-title buttons (the in-card actions), scoped to the Health page so the shell's own links stay out.
    private static List<Button> Actions(Window window) =>
        [.. window.GetVisualDescendants().OfType<HealthPage>().Single().GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("row-title") && b.IsEffectivelyVisible)];

    private static void AssertLink(Button button)
    {
        var what = button.Name ?? button.Content as string ?? "a row-title button";
        Assert.True(button.Classes.Contains("accent-link"), $"{what} has no accent-link class");
        Assert.Equal(TokenColor("Brush.Primary"), BrushColor(button.Foreground));
        Assert.True(button.Focusable && button.IsTabStop, $"{what} can't take keyboard focus");
    }

    private static Color? HoverColor(Window window, Button button)
    {
        ((IPseudoClasses)button.Classes).Set(":pointerover", value: true);
        Settle(window);
        var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().First(p => string.Equals(p.Name, "PART_ContentPresenter", StringComparison.Ordinal));
        var color = BrushColor(presenter.Foreground);
        ((IPseudoClasses)button.Classes).Set(":pointerover", value: false);
        return color;
    }

    [Fact]
    public Task Set_up_and_Open_Agents_rest_in_the_link_colour_and_brighten_under_the_pointer() => OnUi(async () =>
    {
        var (window, _) = await Shown();
        Named<Control>(window, "AgentsCard").BringIntoView();
        Settle(window);
        FrameFile.Save(window, "health-action-links-overview-1600");

        var actions = Actions(window);
        Assert.Contains(actions, b => string.Equals(b.Name, "ProfileSetupLink", StringComparison.Ordinal));
        Assert.Contains(actions, b => string.Equals(b.Name, "AgentsLink", StringComparison.Ordinal));
        Assert.All(actions, AssertLink);
        Assert.All(actions, b => Assert.Equal(TokenColor("Brush.PrimaryHover"), HoverColor(window, b)));
    });

    [Fact]
    public Task View_job_rests_in_the_link_colour_and_brightens_under_the_pointer() => OnUi(async () =>
    {
        var (window, shell) = await Shown();
        shell.Health.ShowServicesTab = true;
        await Until(window, () => Actions(window).Exists(b => string.Equals(b.Content as string, Labels.ViewJob, StringComparison.Ordinal)), "the Services tab never drew View job");
        FrameFile.Save(window, "health-action-links-services-1600");

        var viewJob = Actions(window).First(b => string.Equals(b.Content as string, Labels.ViewJob, StringComparison.Ordinal));
        AssertLink(viewJob);
        Assert.Equal(TokenColor("Brush.PrimaryHover"), HoverColor(window, viewJob));
    });
}
