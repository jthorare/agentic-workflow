using System.Diagnostics.CodeAnalysis;
namespace Aw.Sindri.Ui.ViewModels.Rows;

/// <summary>Shortens a reference in the middle, so its end (the part that tells refs apart) stays.</summary>
[SuppressMessage("Design", "MA0182", Justification = "Stub of the red commit (#2978); the control commit uses it.")]
internal static class RefText
{
    /// <summary>Shortens <paramref name="text"/> to at most <paramref name="maxChars"/> characters, replacing the middle of the first part with an ellipsis and keeping the end.</summary>
    /// <param name="text">The reference, such as acme-platform/exports.</param>
    /// <param name="maxChars">The most characters to return.</param>
    /// <returns>The shortened text, or <paramref name="text"/> when it fits.</returns>
    public static string ShortenMiddle(string text, int maxChars) => throw new NotSupportedException("RefText.ShortenMiddle is not built yet (#2978).");
}
