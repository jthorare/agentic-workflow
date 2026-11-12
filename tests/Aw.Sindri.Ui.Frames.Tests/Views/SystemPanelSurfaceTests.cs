using System.Globalization;
using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The System page's cards are a surface step with no outer border, and their content sits Space.LG 24 inside the card (#2779, inspora note §3).</summary>
[Lane(TestLane.Integration)]
public sealed class SystemPanelSurfaceTests : FrameHost
{
    private const string Body = """{"repos":[{"repo":"repo","state":"ok","commit":"abcdef0123456789abcdef0123456789abcdef01","builtAt":"2026-10-20T12:00:00Z","layers":[{"layer":"structure","state":"ok","code":null,"detail":"ok"}],"counts":null}]}""";

    [Fact]
    public Task A_System_card_has_no_outer_border_and_its_title_sits_24_px_in() => OnUi(async () =>
    {
        Server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, Body);
        var (window, shell) = await Connected(s => s.Health.CodeIndex.Lines.Count > 0);
        shell.GoTo(PageId.System);
        Settle(window);
        Frame(window, "system-panel-surface", "gh2779-");

        var panel = Named<Border>(window, "CodeIndexPanel");
        Assert.Equal(new Thickness(0), panel.BorderThickness);
        var title = Named<TextBlock>(window, "CodeIndexEyebrow");
        var at = title.TranslatePoint(default, panel)!.Value;
        Assert.True(Math.Abs(at.X - 24) < 0.5, string.Create(CultureInfo.InvariantCulture, $"the title starts {at.X} px inside the card, not 24"));
        Assert.True(at.Y >= 23.5, string.Create(CultureInfo.InvariantCulture, $"the title starts {at.Y} px below the card's top, under 24"));
        foreach (var other in window.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("panel") && b.IsEffectivelyVisible))
        {
            Assert.Equal(new Thickness(0), other.BorderThickness);
        }
    });
}
