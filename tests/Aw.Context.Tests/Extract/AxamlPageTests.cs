using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Extract;

[Trait("Lane", "unit")]
public sealed class AxamlPageTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From("aw-sindri");

    private const string Header = "<UserControl xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:vm=\"using:Foo.ViewModels\"";

    private static string View(string className, string? dataType = "vm:SettingsViewModel") =>
        $"{Header} x:Class=\"Foo.Views.{className}\"{(dataType is null ? "" : $" x:DataType=\"{dataType}\"")}>\n  <TextBlock Text=\"hi\" />\n</UserControl>";

    private static ExtractedMap Extract(params RepoFile[] files) => MapExtractor.Extract(_repo, files);

    private static string[] Implementers(ExtractedMap map, string surface) =>
        [.. map.Edges.Where(e => e.Type == EdgeType.Implements && e.To.Value == surface).Select(e => e.From.Value).Order(StringComparer.Ordinal)];

    [Fact]
    public void A_page_axaml_view_is_a_screen_surface_homed_in_its_view_model()
    {
        var map = Extract(
            new RepoFile("src/Ui/Views/SettingsPage.axaml", View("SettingsPage")),
            new RepoFile("src/Ui/Views/SettingsPage.axaml.cs", "namespace Foo.Views; public partial class SettingsPage { }"),
            new RepoFile("src/Ui/ViewModels/SettingsViewModel.cs", "namespace Foo.ViewModels; public sealed class SettingsViewModel { }"),
            new RepoFile("src/Ui/ViewModels/OtherViewModel.cs", "namespace Foo.ViewModels; public sealed class OtherViewModel { }"));

        var page = Assert.Single(map.Nodes, n => n.Key.Value == "surface:screen/SettingsPage");
        Assert.Equal(("SettingsPage", 0.9, Provenance.Deterministic), (page.Label, page.Confidence, page.Provenance));
        Assert.Equal("Settings", PageNames.Offer(page));
        Assert.Equal(["code:src/Ui/ViewModels/SettingsViewModel.cs", "code:src/Ui/Views/SettingsPage.axaml", "code:src/Ui/Views/SettingsPage.axaml.cs"], Implementers(map, "surface:screen/SettingsPage"), StringComparer.Ordinal);
        Assert.True(PageKinds.IsPage(page, map.Edges));
    }

    [Fact]
    public void A_dialog_axaml_view_is_a_dialog_surface()
    {
        var map = Extract(
            new RepoFile("src/Ui/Views/RestoreDialog.axaml", View("RestoreDialog", "vm:RestoreViewModel")),
            new RepoFile("src/Ui/ViewModels/RestoreViewModel.cs", "namespace Foo.ViewModels; public sealed class RestoreViewModel { }"));

        var dialog = Assert.Single(map.Nodes, n => n.Key.Value == "surface:dialog/RestoreDialog");
        Assert.Equal("Restore", PageNames.Offer(dialog));
        Assert.Equal(["code:src/Ui/ViewModels/RestoreViewModel.cs", "code:src/Ui/Views/RestoreDialog.axaml"], Implementers(map, "surface:dialog/RestoreDialog"), StringComparer.Ordinal);
        Assert.True(PageKinds.IsPage(dialog, map.Edges));
    }

    [Theory]
    [InlineData("src/Ui/Views/MainWindow.axaml", "<Window xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" x:Class=\"Foo.Views.MainWindow\" />")]
    [InlineData("src/Ui/Views/Controls/ZoomBorder.axaml", "<UserControl xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" x:Class=\"Foo.Views.Controls.ZoomBorder\" />")]
    [InlineData("src/Ui/Styles/Theme.axaml", "<Styles xmlns=\"https://github.com/avaloniaui\"><Style Selector=\"Button\" /></Styles>")]
    [InlineData("src/Ui/Styles/Colors.axaml", "<ResourceDictionary xmlns=\"https://github.com/avaloniaui\" />")]
    public void An_axaml_window_or_control_makes_no_surface(string path, string text)
    {
        var map = Extract(new RepoFile(path, text));

        Assert.DoesNotContain(map.Nodes, n => n.Key.Kind == NodeKind.Surface);
    }

    [Fact]
    public void An_axaml_view_without_a_data_type_still_makes_its_page()
    {
        var map = Extract(
            new RepoFile("src/Ui/Views/AboutPage.axaml", View("AboutPage", dataType: null)),
            new RepoFile("src/Ui/Views/AboutPage.axaml.cs", "namespace Foo.Views; public partial class AboutPage { }"),
            new RepoFile("src/Ui/ViewModels/MissingViewModel.cs", "namespace Foo.ViewModels; public sealed class MissingViewModel { }"));

        _ = Assert.Single(map.Nodes, n => n.Key.Value == "surface:screen/AboutPage");
        Assert.Equal(["code:src/Ui/Views/AboutPage.axaml", "code:src/Ui/Views/AboutPage.axaml.cs"], Implementers(map, "surface:screen/AboutPage"), StringComparer.Ordinal);
    }
}
