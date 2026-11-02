using System.Globalization;
using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.Views;
using Aw.Sindri.Ui.Views.Setup;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>An open setup step's body keeps the same right inset as its header, so its meter and lines never touch the card border (#1069).</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class SetupStepRowLayoutTests : FrameHost
{
    private const double Inset = 12;

    [Fact]
    public Task Step_body_ends_inside_the_card_on_the_right() => OnUi(async () =>
    {
        Server.SetupAnswer = new RouteAnswer(HttpStatusCode.OK, SetupFixture.WithProgress(SetupFixture.Golden("failed"), "map", 40, 100, "files").ToJsonString());
        var (window, shell) = await Connected(s => s.Setup.Steps.Count > 0, 741, 900);
        window.SettingsSlot.Content = new SettingsView { DataContext = new SettingsViewModel(new Preferences(new InMemoryPreferencesStore()), Motion, _ => { }, shell.Setup) };
        shell.GoTo(PageId.Settings);
        Settle(window);

        var row = window.GetVisualDescendants().OfType<SetupStepRow>().First(r => r.IsEffectivelyVisible && r.GetVisualDescendants().OfType<ProgressBar>().Any(p => p.IsEffectivelyVisible));
        var card = row.GetVisualDescendants().OfType<Expander>().First(e => string.Equals(e.Name, "SetupStepExpander", StringComparison.Ordinal));
        var body = Named<StackPanel>(row, "SetupStepBody");

        var bodyRight = body.TranslatePoint(new Point(body.Bounds.Width, 0), card)!.Value.X;

        Assert.True(card.Bounds.Width - bodyRight >= Inset, string.Create(CultureInfo.InvariantCulture, $"the body ends at {bodyRight} in a card {card.Bounds.Width} wide"));
    });
}
