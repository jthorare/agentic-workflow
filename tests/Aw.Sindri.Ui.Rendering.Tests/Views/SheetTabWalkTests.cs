using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.Views.NeedsYou;

namespace Aw.Sindri.Ui.Rendering.Tests.Views;

/// <summary>Tab beside an open decision sheet (review re-proof B8): every stop is a usable, visible control that draws a ring.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class SheetTabWalkTests : FrameHost
{
    private static int RingPixels(Window window)
    {
        using var bitmap = window.CaptureRenderedFrame()!;
        var size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(stride * size.Height);
        try
        {
            bitmap.CopyPixels(new Avalonia.PixelRect(size), buffer, stride * size.Height, stride);
            var bytes = new byte[stride * size.Height];
            System.Runtime.InteropServices.Marshal.Copy(buffer, bytes, 0, bytes.Length);
            var rgba = bitmap.Format == Avalonia.Platform.PixelFormat.Rgba8888;
            var n = 0;
            for (var i = 0; i < bytes.Length; i += 4)
            {
                var (r, b) = rgba ? (bytes[i], bytes[i + 2]) : (bytes[i + 2], bytes[i]);
                if (Math.Abs(r - 0x82) <= 6 && Math.Abs(bytes[i + 1] - 0x8f) <= 6 && Math.Abs(b - 0xff) <= 6)
                {
                    n++;
                }
            }

            return n;
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
        }
    }

    [Fact]
    public Task Every_stop_beside_an_open_decision_sheet_is_usable_visible_and_ringed() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Decision(), NeedsYouRows.Decision("decision:DEC-2")], ""));
        shell.MarkAllSeen();   // a pulsing row fades its own ring: the walk looks at the ring, not the pulse
        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-1"));
        Settle(window);
        var accept = window.GetVisualDescendants().OfType<NeedsYouRow>().First().GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("command-control"));
        Assert.True(accept.Focus(NavigationMethod.Tab));
        var walk = new List<string>();
        var wrong = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, keySymbol: null);
            Settle(window);
            var focused = window.FocusManager?.GetFocusedElement() as Control;
            walk.Add(focused is null ? "<nothing>" : focused.DataContext?.GetType().Name + ":" + Avalonia.Automation.AutomationProperties.GetName(focused));
            if (focused is not { IsEffectivelyVisible: true, IsEffectivelyEnabled: true, Bounds.Width: > 0 } || !focused.Classes.Contains(":focus-visible") || RingPixels(window) < 100)
            {
                wrong.Add(walk[^1]);
            }
        }

        Assert.Empty(wrong);
        Assert.Equal(["NeedsYouActionControl:Decline REQ-01JD4", "DecisionSheetViewModel:" + Labels.DecisionNoteLabel, "DecisionOption:Accept DEC-1", "DecisionOption:Decline DEC-1"], walk);
    });
}
