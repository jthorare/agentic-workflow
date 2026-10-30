using Avalonia.Input;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The keys that cycle the window's regions (UI spec §8, §11.2): F6 and Shift+F6 everywhere; on macOS also ⌘⌥→ and ⌘⌥←
/// (owner decision, 2026-10-12, review run 4: F6 is the Focus key there and reaches the app only with fn held). Checked
/// for both platforms on every OS, so CI covers the macOS binding too.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RegionKeysTests
{
    [Theory]
    [InlineData(Key.F6, KeyModifiers.None, false, 1)]
    [InlineData(Key.F6, KeyModifiers.Shift, false, -1)]
    [InlineData(Key.F6, KeyModifiers.None, true, 1)]
    [InlineData(Key.F6, KeyModifiers.Shift, true, -1)]
    [InlineData(Key.Right, KeyModifiers.Meta | KeyModifiers.Alt, true, 1)]
    [InlineData(Key.Left, KeyModifiers.Meta | KeyModifiers.Alt, true, -1)]
    public void A_region_key_steps_the_cycle(Key key, KeyModifiers modifiers, bool macOS, int step) =>
        Assert.Equal(step, ShellWindow.RegionStep(key, modifiers, macOS));

    // Not on other platforms (Ctrl+Alt+arrows switch workspaces on Linux), and only with exactly ⌘⌥ on macOS: ⌘← and
    // ⌥← are text navigation, and Ctrl+F6 isn't bound (the owner's decision).
    [Theory]
    [InlineData(Key.Right, KeyModifiers.Meta | KeyModifiers.Alt, false)]
    [InlineData(Key.Left, KeyModifiers.Meta | KeyModifiers.Alt, false)]
    [InlineData(Key.Right, KeyModifiers.Control | KeyModifiers.Alt, false)]
    [InlineData(Key.Right, KeyModifiers.Meta, true)]
    [InlineData(Key.Left, KeyModifiers.Alt, true)]
    [InlineData(Key.Left, KeyModifiers.Meta | KeyModifiers.Alt | KeyModifiers.Shift, true)]
    [InlineData(Key.Right, KeyModifiers.None, true)]
    [InlineData(Key.F6, KeyModifiers.Control, true)]
    [InlineData(Key.F6, KeyModifiers.Control, false)]
    public void Other_keys_leave_the_cycle_alone(Key key, KeyModifiers modifiers, bool macOS) =>
        Assert.Null(ShellWindow.RegionStep(key, modifiers, macOS));
}
