using System.Runtime.InteropServices;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>A box in the graph's world, in pixels.</summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct GraphBox(double X, double Y, double Width, double Height)
{
    /// <summary>The right edge.</summary>
    internal double Right => X + Width;

    /// <summary>The bottom edge.</summary>
    internal double Bottom => Y + Height;

    /// <summary>True when the two boxes share any area; boxes that only touch at an edge do not.</summary>
    /// <param name="other">The other box.</param>
    /// <returns>True when they overlap.</returns>
    internal bool Overlaps(GraphBox other) => X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;
}
