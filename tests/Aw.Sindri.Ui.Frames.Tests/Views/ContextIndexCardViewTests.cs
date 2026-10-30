using System.Net;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>Health's Context index card in the window (#1597): accessible names, and its place right after the Code index card.</summary>
[Lane(TestLane.Integration)]
public sealed class ContextIndexCardViewTests : FrameHost
{
    private const string Body = """{"sources":[{"source":"github","phase":"live","usable":true,"complete":false,"lastSync":null,"lastCode":null,"pull":"running","records":200,"lastCommit":"2026-10-11T11:57:00Z"},{"source":"linear","phase":"live","usable":false,"complete":false,"lastSync":null,"lastCode":null,"pull":"idle","records":0,"lastCommit":null}]}""";

    [Fact]
    public Task The_card_names_each_line_and_the_heading_for_assistive_technology() => OnUi(async () =>
    {
        Server.IndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, Body);
        var (window, shell) = await Connected(s => s.Health.ContextIndex.Lines.Count > 0);
        shell.GoTo(PageId.System);
        Settle(window);

        var lines = Named<ItemsControl>(window, "ContextIndexLines").GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal(shell.Health.ContextIndex.Lines[1].Name, AutomationProperties.GetName(lines[1]));
        Assert.StartsWith("Context index: linear: idle", AutomationProperties.GetName(lines[1]), StringComparison.Ordinal);
        Assert.Equal(("Context index", "needs a look"), (AutomationProperties.GetName(Named<TextBlock>(window, "ContextIndexEyebrow")), AutomationProperties.GetName(Named<TextBlock>(window, "ContextIndexWord"))));
        Assert.False(Named<TextBlock>(window, "ContextIndexMessage").IsVisible);
    });

    [Fact]
    public Task The_card_sits_right_after_the_Code_index() => OnUi(async () =>
    {
        Server.IndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, Body);
        var (window, shell) = await Connected(s => s.Health.ContextIndex.Lines.Count > 0);
        shell.GoTo(PageId.System);
        Settle(window);

        var cards = Named<WrapPanel>(window, "SystemCards").Children;
        Assert.Equal(cards.IndexOf(Named<Border>(window, "CodeIndexPanel")) + 1, cards.IndexOf(Named<Border>(window, "ContextIndexPanel")));
    });
}
