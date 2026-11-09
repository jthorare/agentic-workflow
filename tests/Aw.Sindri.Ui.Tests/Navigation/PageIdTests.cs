using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Palette;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Navigation;

/// <summary>The app's pages after the recipe pages went (v2 PR 4): navigation and the palette's Go to list carry no Recipes page; Workflows came back as the graph workflows' page (PR 17).</summary>
[Lane(TestLane.Unit)]
public sealed class PageIdTests
{
    [Fact]
    public void The_app_has_no_Recipes_page_and_a_Workflows_page()
    {
        Assert.DoesNotContain("Recipes", Enum.GetNames<PageId>(), StringComparer.Ordinal);
        Assert.Contains("Workflows", Enum.GetNames<PageId>(), StringComparer.Ordinal);
    }

    [Fact]
    public void The_palettes_Go_to_list_names_Workflows_and_not_Recipes()
    {
        var palette = new CommandPaletteViewModel(new CatalogViewModel(new FakeClipboard(), new FakeTimeProvider(A.Noon)), _ => { }, _ => { });
        palette.Open();

        var goTo = palette.Entries.Where(e => !e.IsAction).Select(e => e.Label).ToList();

        Assert.DoesNotContain(goTo, label => label.Contains("Recipes", StringComparison.Ordinal));
        Assert.Contains(goTo, label => label.Contains("Workflows", StringComparison.Ordinal));
    }
}
