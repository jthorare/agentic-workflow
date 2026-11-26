using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Theme;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// #4402: the collapsed Todo card on Home is five row slots and the Show all slot tall, in Row.Height.Default units. TodoPanel read that resource once, so a live text-scale change in Settings (TextScale.Apply rewrites
/// the resources) left the card at the old height until Home was built again. After the change the card is as tall as one built fresh at the new scale.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class TodoPanelTextScaleFramesTests : FrameHost
{
    private const double NewScale = 1.3;

    private static double CardHeight(Window window) => Named<Control>(window, "TodoPanelFrame").Bounds.Height;

    [Fact]
    public Task The_collapsed_card_follows_a_live_text_scale_change_to_the_height_a_fresh_window_has() => OnUi(async () =>
    {
        var resources = Application.Current!.Resources;
        try
        {
            TextScale.Apply(resources, 1.0);
            var (window, _) = await Connected(width: 1280, height: 800);
            Settle(window);
            var before = CardHeight(window);

            TextScale.Apply(resources, NewScale);
            Settle(window);
            var live = CardHeight(window);
            window.Close();

            var (fresh, _) = await Connected(width: 1280, height: 800);
            Settle(fresh);
            var rebuilt = CardHeight(fresh);

            var culture = CultureInfo.InvariantCulture;
            Assert.True(rebuilt > before, string.Create(culture, $"the card at {NewScale} ({rebuilt} px) is not taller than at 1.0 ({before} px): the test would prove nothing"));
            Assert.True(Math.Abs(live - rebuilt) <= 0.5, string.Create(culture, $"after the live change the card is {live} px tall; built fresh at {NewScale} it is {rebuilt} px"));
        }
        finally
        {
            TextScale.Apply(resources, 1.0);
        }
    });
}
