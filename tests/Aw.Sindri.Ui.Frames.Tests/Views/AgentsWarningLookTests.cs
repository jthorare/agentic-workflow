using System.Globalization;
using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Icons;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The Agents page's warning lines look like warnings (#4463; agents spec §2: "one amber line per problems[] entry", "the problem and stale lines as the amber
/// strip"). Before the fix the problem-line class had no style and the stale text's attention class was styled only on Home, so both drew in plain ink.
/// A problem row is a strip in the status chip's attention tokens, and the stale text is in the attention colour. Each frame is saved before the check, under
/// frames/ as agents-warning-*.png when AW_UI_FRAMES=1.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class AgentsWarningLookTests : FrameHost
{
    private const string Aged = "The agent list is from 2 min ago: the last 3 passes took longer than their 500 ms budget";

    private static ISolidColorBrush Brush(string key) =>
        Application.Current is { } app && app.TryFindResource(key, app.ActualThemeVariant, out var value) && value is ISolidColorBrush brush ? brush : throw new InvalidOperationException($"{key} is not a brush token");

    private static void AssertBrush(string key, IBrush? actual, string what)
    {
        var expected = Brush(key);
        var brush = Assert.IsType<ISolidColorBrush>(actual, exactMatch: false);
        Assert.True(expected.Color == brush.Color && Math.Abs(expected.Opacity - brush.Opacity) < 0.001,
            string.Create(CultureInfo.InvariantCulture, $"{what} is {brush.Color} at {brush.Opacity}, not {key}"));
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(1440)]
    public Task A_problem_row_is_an_amber_strip_with_amber_text(int width) => OnUi(async () =>
    {
        var body = GoldenFiles.Api("sessions", "problems-source-failed")
            .Replace("\"claude-records\"", "\"collector\"", StringComparison.Ordinal)
            .Replace("The claude-records source couldn't be read in the last pass", Aged, StringComparison.Ordinal);
        Server.SessionAnswer = new RouteAnswer(HttpStatusCode.OK, body);
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Agents);
        await Until(window, () => shell.Agents.ProblemLines.Count > 0 && shell.Agents.Groups.Count > 0);

        Frame(window, string.Create(CultureInfo.InvariantCulture, $"problem-{width}"), "agents-warning-");
        var strip = Named<ItemsControl>(window, "AgentsProblems").GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("problem-line"));
        AssertBrush("Brush.Status.Attention.Bg", strip.Background, "the strip's fill");
        AssertBrush("Brush.Status.Attention.Border", strip.BorderBrush, "the strip's edge");
        Assert.True(strip.BorderThickness.Left > 0, "the strip has no edge");
        var text = strip.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == Aged);
        AssertBrush("Brush.Status.Attention.Fg", text.Foreground, "the row's text");
        Assert.True(strip.GetVisualDescendants().OfType<StateIcon>().Any(i => i.IsEffectivelyVisible), "the strip has no warn glyph");
    });

    [Fact]
    public Task The_stale_line_is_in_the_attention_colour() => OnUi(async () =>
    {
        Server.SessionAnswer = RouteAnswer.Api("sessions", "fixture-three-providers");
        var (window, shell) = await Connected(width: 1000, height: 900);
        shell.GoTo(PageId.Agents);
        await Until(window, () => shell.Agents.Groups.Count > 0);
        Server.SessionAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-TEST-500", "boom");
        await shell.Agents.Refresh(shell.Health.ApiUrl()!, TestContext.Current.CancellationToken);
        await Until(window, () => Visible(window, "AgentsStale"));

        Frame(window, "stale-1000", "agents-warning-");
        AssertBrush("Brush.Attention", Named<TextBlock>(window, "AgentsStaleText").Foreground, "the stale text");
    });
}
