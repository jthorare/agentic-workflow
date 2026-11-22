using Avalonia.Input;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Agents;

namespace Aw.Sindri.Ui.Tests.Agents.Terminal;

/// <summary>
/// Key names for the pane's routes: Avalonia's Key enum gives two names to one value (PageUp and Prior, PageDown and Next, Enter and Return, CapsLock and
/// Capital, PrintScreen and Snapshot), and Key.ToString() may answer either. A key with an alias is named from its value, so "Shift+PageUp" never turns up
/// as "Shift+Prior" and Shift+PageUp keeps scrolling the pane.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class TerminalKeyNamesTests
{
    [Fact]
    public void PageUp_and_Prior_are_both_named_PageUp() =>
        Assert.Equal(("Shift+PageUp", "Shift+PageUp", "PageUp"), (TerminalKeyNames.From(Key.PageUp, KeyModifiers.Shift), TerminalKeyNames.From(Key.Prior, KeyModifiers.Shift), TerminalKeyNames.From(Key.PageUp, KeyModifiers.None)));

    [Fact]
    public void PageDown_and_Next_are_both_named_PageDown() =>
        Assert.Equal(("Shift+PageDown", "Shift+PageDown", "PageDown"), (TerminalKeyNames.From(Key.PageDown, KeyModifiers.Shift), TerminalKeyNames.From(Key.Next, KeyModifiers.Shift), TerminalKeyNames.From(Key.PageDown, KeyModifiers.None)));

    [Fact]
    public void Enter_and_Return_are_both_named_Enter() =>
        Assert.Equal(("Enter", "Enter", "Ctrl+Enter"), (TerminalKeyNames.From(Key.Enter, KeyModifiers.None), TerminalKeyNames.From(Key.Return, KeyModifiers.None), TerminalKeyNames.From(Key.Return, KeyModifiers.Control)));

    [Fact]
    public void CapsLock_and_Capital_are_both_named_CapsLock() =>
        Assert.Equal(("CapsLock", "CapsLock"), (TerminalKeyNames.From(Key.CapsLock, KeyModifiers.None), TerminalKeyNames.From(Key.Capital, KeyModifiers.None)));

    [Fact]
    public void PrintScreen_and_Snapshot_are_both_named_PrintScreen() =>
        Assert.Equal(("PrintScreen", "PrintScreen"), (TerminalKeyNames.From(Key.PrintScreen, KeyModifiers.None), TerminalKeyNames.From(Key.Snapshot, KeyModifiers.None)));

    [Fact]
    public void A_key_with_no_alias_keeps_its_own_name() =>
        Assert.Equal(("F6", "Shift+Tab", "Ctrl+Shift+L", "Esc"), (TerminalKeyNames.From(Key.F6, KeyModifiers.None), TerminalKeyNames.From(Key.Tab, KeyModifiers.Shift), TerminalKeyNames.From(Key.L, KeyModifiers.Control | KeyModifiers.Shift), TerminalKeyNames.From(Key.Escape, KeyModifiers.None)));
}
