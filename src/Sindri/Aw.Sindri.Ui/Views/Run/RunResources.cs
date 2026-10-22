using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Aw.Sindri.Ui.Views.Run;

/// <summary>Reads the theme's brushes and radii for the run page's code-built views (a state's brush is named by a key, so it cannot be bound in XAML).</summary>
internal static class RunResources
{
    /// <summary>A brush by key.</summary>
    /// <param name="control">A control in the tree whose theme is read.</param>
    /// <param name="key">The resource key (Brush.*).</param>
    /// <returns>The brush; grey when the key is not found.</returns>
    internal static IBrush Brush(this Control control, string key) =>
        control.TryFindResource(key, control.ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Gray;

    /// <summary>A corner radius by key.</summary>
    /// <param name="control">A control in the tree whose theme is read.</param>
    /// <param name="key">The resource key (Radius.*).</param>
    /// <param name="fallback">What to use when the key is not found.</param>
    /// <returns>The radius.</returns>
    internal static CornerRadius Corner(this Control control, string key, double fallback) =>
        control.TryFindResource(key, control.ActualThemeVariant, out var value) && value is CornerRadius radius ? radius : new CornerRadius(fallback);

    /// <summary>A text block in the page's type.</summary>
    /// <param name="text">The words.</param>
    /// <param name="size">The font size.</param>
    /// <param name="line">The line height.</param>
    /// <param name="weight">The weight.</param>
    /// <param name="brush">The foreground.</param>
    /// <returns>The text block.</returns>
    internal static TextBlock Text(string text, double size, double line, FontWeight weight, IBrush brush) =>
        new() { Text = text, FontSize = size, LineHeight = line, FontWeight = weight, Foreground = brush, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap };
}
