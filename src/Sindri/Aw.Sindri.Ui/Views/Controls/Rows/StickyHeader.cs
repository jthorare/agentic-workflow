using System.Diagnostics.CodeAnalysis;
namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>The push-out math of a sticky group header, shared by <see cref="StickyGroupList"/> and <see cref="RowList"/> (#2978). Stub.</summary>
[SuppressMessage("Design", "MA0182", Justification = "Stub of the red commit (#2978); the control commit uses it.")]
internal static class StickyHeader
{
    /// <summary>How far the sticky header is pushed up by the next group's header: 0 until the next header reaches it, then negative.</summary>
    /// <param name="nextTop">The next group's top in the scrolled content, or positive infinity when there is none.</param>
    /// <param name="offset">The scroll offset.</param>
    /// <param name="height">The header's height.</param>
    /// <returns>The vertical shift to apply, zero or less.</returns>
    public static double PushOut(double nextTop, double offset, double height) => throw new NotSupportedException("StickyHeader.PushOut is not built yet (#2978).");
}
