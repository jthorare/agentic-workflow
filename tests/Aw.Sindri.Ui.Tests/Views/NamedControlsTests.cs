using System.Reflection;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Theme;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// Every view's x:Name fields are set once it is constructed: a view loads its XAML through the generated
/// InitializeComponent, which binds them, never through AvaloniaXamlLoader.Load(this), which leaves them null.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class NamedControlsTests
{
    private static readonly XNamespace _x = "http://schemas.microsoft.com/winfx/2006/xaml";

    // Every view the library has, found from its XAML (a root with an x:Class that is a Control), never a list kept by hand.
    private static readonly (string Path, Type Type)[] _views =
    [
        .. UiSource.Axaml
            .Where(f => f.StartsWith(Path.Combine(UiSource.Library, "Views") + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Select(f => (Path: Path.GetRelativePath(Path.Combine(UiSource.Library, "Views"), f)[..^".axaml".Length].Replace('\\', '/'), Class: (string?)XDocument.Load(f).Root?.Attribute(_x + "Class")))
            .Where(v => v.Class is not null)
            .Select(v => (v.Path, Type: typeof(ShellWindow).Assembly.GetType(v.Class!, throwOnError: true)!))
            .Where(v => typeof(Control).IsAssignableFrom(v.Type))
            .OrderBy(v => v.Path, StringComparer.Ordinal),
    ];

    public static TheoryData<string> Views => [.. _views.Select(v => v.Path)];

    internal static Control Build(string view) => Construct(view);

    private static Control Construct(string view) =>
        (Control)Activator.CreateInstance(_views.Single(v => v.Path == view).Type, nonPublic: true)!;

    [Theory]
    [MemberData(nameof(Views))]
    public async Task Every_named_control_is_set_after_construction(string view)
    {
        var names = XDocument.Load(Path.Combine(UiSource.Library, "Views", view + ".axaml"))
            .Descendants().Where(e => !e.Ancestors().Any(a => a.Name.LocalName.EndsWith("Template", StringComparison.Ordinal)))   // a name inside a template (PART_ContentPresenter) belongs to the template's own scope and gets no field
            .Select(e => (string?)e.Attribute(_x + "Name")).OfType<string>().ToList();

        var unset = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(NamedControlsTests).Assembly)
            .Dispatch(() =>
            {
                var control = Construct(view);
                return names.Where(n => control.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(control) is null).ToList();
            }, TestContext.Current.CancellationToken);

        Assert.Empty(unset);
    }
}
