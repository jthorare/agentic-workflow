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
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.Views.Icons;

namespace Aw.Sindri.Ui.Frames.Panels.Tests.Views;

/// <summary>
/// The System page's Code index and Views cards draw a mark beside their word (#4331), as Control plane and Context index do: the green check for "ok" and
/// "Up to date", the attention mark for "needs a look" and "Stalled", at 1000 and 1280. Each frame is saved before the checks, so the same file run on a
/// tree without #4331 writes the before frames and fails on the missing mark.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class HealthMarksFramesTests : FrameHost
{
    private static string CodeIndex(string healthState, bool needsLook) =>
        $$"""{"repos":[{"repo":"repo","state":"ok","commit":"abcdef0123456789abcdef0123456789abcdef01","builtAt":"2026-10-20T12:00:00Z","layers":[{"layer":"structure","state":"ok","code":null,"detail":"ok"}],"counts":null}],"health":[{"repo":"repo","state":"{{healthState}}","embeddingsPercent":null,"waiting":null,"needsLook":{{(needsLook ? "true" : "false")}}}]}""";

    // The System page with both cards read and the Views card in view.
    private async Task<Window> Shown(string codeIndex, string services, string viewsWord, double width)
    {
        Server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, codeIndex);
        Server.ServiceView = RouteAnswer.Api("services", services);
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.System);
        await Until(window, () => shell.Health.CodeIndex.Lines.Count > 0 && string.Equals(shell.Health.ViewsWord, viewsWord, StringComparison.Ordinal),
            "the Code index and Views cards never read");
        Named<Control>(window, "ViewsCard").BringIntoView();
        Settle(window);
        return window;
    }

    private static StateIcon Mark(Window window, string name) =>
        window.GetVisualDescendants().OfType<StateIcon>().FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(name + " is not drawn");

    private static Color TokenColor(string key) =>
        Application.Current is { } app && app.TryFindResource(key, app.ActualThemeVariant, out var value) && value is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException(key + " is not a brush token");

    private static void AssertMark(Window window, string name, StateGlyph glyph, string brush)
    {
        var icon = Mark(window, name);
        Assert.True(icon.IsEffectivelyVisible, name + " is hidden");
        Assert.Equal(glyph, icon.Glyph);
        Assert.Equal(TokenColor(brush), Assert.IsType<ISolidColorBrush>(icon.Foreground, exactMatch: false).Color);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(1280)]
    public Task Ok_and_up_to_date_draw_the_green_check(double width) => OnUi(async () =>
    {
        var window = await Shown(CodeIndex("ok", needsLook: false), "approved", Labels.ViewsUpToDate, width);
        FrameFile.Save(window, string.Create(CultureInfo.InvariantCulture, $"health-marks-ok-{width}"));

        AssertMark(window, "CodeIndexGlyph", StateGlyph.Ok, "Brush.Success");
        AssertMark(window, "ViewsGlyph", StateGlyph.Ok, "Brush.Success");
    });

    [Theory]
    [InlineData(1000)]
    [InlineData(1280)]
    public Task Needs_a_look_and_stalled_draw_the_attention_mark(double width) => OnUi(async () =>
    {
        var window = await Shown(CodeIndex("stale", needsLook: true), "stalled", Labels.ViewsStalled, width);
        FrameFile.Save(window, string.Create(CultureInfo.InvariantCulture, $"health-marks-look-{width}"));

        AssertMark(window, "CodeIndexGlyph", StateGlyph.Warning, "Brush.Attention");
        AssertMark(window, "ViewsGlyph", StateGlyph.Warning, "Brush.Attention");
    });
}
