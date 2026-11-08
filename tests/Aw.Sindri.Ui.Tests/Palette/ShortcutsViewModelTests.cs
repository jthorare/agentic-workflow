using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Palette;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Palette;

/// <summary>The `?` sheet (UI spec §8): the fixed keys and the catalog rows' own shortcuts.</summary>
[Lane(TestLane.Unit)]
public sealed class ShortcutsViewModelTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly CatalogViewModel _catalog = new(new FakeClipboard(), new FakeTimeProvider(A.Noon));

    private static CommandRowView Row(string action, string? shortcut) =>
        new(action, "desk", ["human"], Terminal: false, "POST", "/x", "sindri x", "ready", Destructive: false, ReadsViews: false, shortcut);

    [Fact]
    public void The_sheet_lists_the_fixed_keys_of_section_8()
    {
        var sheet = new ShortcutsViewModel(_catalog);

        sheet.Open();

        Assert.True(sheet.IsOpen);
        Assert.Contains(("⌘K / Ctrl+K", "Command palette"), sheet.Rows.Select(r => (r.Keys, r.Action)));
        Assert.Contains(("?", "Shortcuts sheet"), sheet.Rows.Select(r => (r.Keys, r.Action)));
        Assert.Contains(("⌘, / Ctrl+,", "Settings"), sheet.Rows.Select(r => (r.Keys, r.Action)));
        Assert.Contains(sheet.Rows, r => r.Keys.StartsWith("G then", StringComparison.Ordinal));
        Assert.All(sheet.Rows, r => Assert.Equal(r.Keys + ": " + r.Action, r.Name));
    }

    [Fact]
    public void The_sheet_lists_the_todo_keys()
    {
        var sheet = new ShortcutsViewModel(_catalog);

        sheet.Open();

        var rows = sheet.Rows.Select(r => (r.Keys, r.Action)).ToList();
        Assert.Contains(("N", "New todo"), rows);
        Assert.Contains(("⌘⇧N / Ctrl+Shift+N", "New todo from anywhere"), rows);
        Assert.Contains(("⌘Z / Ctrl+Z", "Undo the last action"), rows);
        Assert.Contains("T Todo on Home", sheet.Rows.Single(r => r.Keys.StartsWith("G then", StringComparison.Ordinal)).Action, StringComparison.Ordinal);
    }

    // E-8: a catalog row's shortcut is listed with its label.
    [Fact]
    public void A_catalog_shortcut_is_listed_with_its_label()
    {
        _catalog.Show(_api, new FakeSindriApi(), new CommandList([Row("CancelJob", "Shift+X"), Row("RunService", shortcut: null)]));
        var sheet = new ShortcutsViewModel(_catalog);

        sheet.Open();

        Assert.Contains(("Shift+X", "Cancel job"), sheet.Rows.Select(r => (r.Keys, r.Action)));
        Assert.DoesNotContain(sheet.Rows, r => r.Action == "Run now");
    }

    [Fact]
    public void Close_hides_it()
    {
        var sheet = new ShortcutsViewModel(_catalog);
        sheet.Open();

        sheet.Close();

        Assert.False(sheet.IsOpen);
    }
}
