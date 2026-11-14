using Avalonia.Media.Immutable;

namespace Aw.Sindri.Ui.Terminal;

/// <summary>One brush per terminal colour, made on first use and kept: a frame has thousands of cells and a handful of colours, so painting must not make a brush per cell.</summary>
internal sealed class TerminalBrushCache
{
    private readonly Dictionary<TerminalColor, ImmutableSolidColorBrush> _brushes = [];

    /// <summary>Gets how many brushes this cache has made (a test seam: a second paint of the same frame must leave it unchanged).</summary>
    internal int Created { get; private set; }

    /// <summary>The brush of a colour.</summary>
    /// <param name="color">The cell's colour.</param>
    /// <returns>The brush, or null for the terminal default (the painter takes it from the theme).</returns>
    internal ImmutableSolidColorBrush? Get(TerminalColor color)
    {
        if (color.Kind == TerminalColorKind.Default)
        {
            return null;
        }

        if (!_brushes.TryGetValue(color, out var brush))
        {
            brush = new ImmutableSolidColorBrush(TerminalPalette.Resolve(color) ?? default);
            _brushes[color] = brush;
            Created++;
        }

        return brush;
    }
}
