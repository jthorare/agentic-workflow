using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Theme;

namespace Aw.Sindri.Ui.Tests.Theme;

/// <summary>The theme as the running app resolves it (SindriApp under the headless platform): tokens, the Fluent palette, the overrides, the embedded mono font and the text scale.</summary>
[Lane(TestLane.Integration)]
public sealed class ThemeResourcesTests
{
    private static readonly TokenFile _tokens = TokenFile.Load();
    private static readonly string[] _brushes = ["Canvas", "Surface1", "Ink", "InkSubtle", "Primary", "PrimaryHover", "Attention", "Error", "Violet", "VioletSoft"];

    private static async Task OnUi(Action body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ThemeResourcesTests).Assembly)
            .Dispatch(() =>
            {
                body();
                return true;
            }, TestContext.Current.CancellationToken);

    private static object? Resource(string key) =>
        Application.Current!.TryGetResource(key, ThemeVariant.Dark, out var value) ? value : null; // why: the headless session starts the app

    private static string Hex(Color c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";

    [Fact]
    public Task The_app_resolves_every_brush_token_to_its_colour() => OnUi(() =>
    {
        foreach (var name in _brushes)
        {
            var brush = Assert.IsType<SolidColorBrush>(Resource("Brush." + name), exactMatch: false);
            Assert.Equal(_tokens.Color(name), Hex(brush.Color));
        }
    });

    [Fact]
    public Task The_Fluent_dark_palette_and_overrides_win_over_the_stock_theme() => OnUi(() =>
    {
        Assert.Equal(_tokens.Color("Primary"), Hex(Assert.IsType<Color>(Resource("SystemAccentColor"))));
        Assert.Equal(_tokens.Color("PrimaryHoverFill"), Hex(Assert.IsType<SolidColorBrush>(Resource("AccentButtonBackgroundPointerOver"), exactMatch: false).Color));
        Assert.Equal(new CornerRadius(8), Resource("ControlCornerRadius"));
    });

    [Fact]
    public Task The_embedded_mono_font_loads() => OnUi(() =>
    {
        var family = Assert.IsType<FontFamily>(Resource("Font.Mono"));

        Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface(family), out var glyphs));
        Assert.Equal("JetBrains Mono", glyphs.FamilyName);
    });

    [Fact]
    public Task The_text_scale_scales_every_step_from_its_base_and_clamps() => OnUi(() =>
    {
        try
        {
            TextScale.Apply(Application.Current!.Resources, 1.2); // why: the headless session starts the app
            Assert.Equal(1.2, Resource("Type.Scale"));
            Assert.Equal(13 * 1.2, (double)Resource("Type.Body.Size")!, 6); // why: set just above
            Assert.Equal(32 * 1.2, (double)Resource("Type.PageTitle.LineHeight")!, 6); // why: set just above

            Assert.Equal(16 * 1.2, (double)Resource("Size.Icon")!, 6); // why: set just above; R1, icons scale with the text

            TextScale.Apply(Application.Current!.Resources, 3); // why: as above
            Assert.Equal(1.3, Resource("Type.Scale"));
            Assert.Equal(13 * 1.3, (double)Resource("Type.Body.Size")!, 6); // why: set just above
        }
        finally
        {
            TextScale.Apply(Application.Current!.Resources, 1); // why: as above
        }
    });

    // R1 (§11.3): the state glyphs and the shell's marks are vector geometries in Theme/Icons.axaml, merged into the app.
    [Fact]
    public Task Every_icon_is_a_geometry_resource() => OnUi(() =>
    {
        foreach (var name in (string[])["Ok", "Down", "Stale", "Profile", "Brand", "Refresh", "Spinner", "Command", "Close", "Kind.Feature", "Kind.Screen", "Kind.Route", "Kind.Dialog", "Kind.Email", "Kind.Action", "Kind.Data", "Kind.File", "Kind.Test", "Kind.Ticket", "Kind.Decision", "Kind.Dot", "Kind.Warn", "Kind.Chevron", "Kind.Plus", "Kind.Minus", "Kind.Fit"])
        {
            _ = Assert.IsType<Geometry>(Resource("Icon." + name), exactMatch: false);
        }

        Assert.Equal(16d, Resource("Size.Icon"));
    });
}
