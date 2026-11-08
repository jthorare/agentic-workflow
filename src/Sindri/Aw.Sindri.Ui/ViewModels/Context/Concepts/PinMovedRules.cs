using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>The §3.8 "What moved" line of a pinned card as one pure function.</summary>
internal static class PinMovedRules
{
    /// <summary>The what-moved line.</summary>
    /// <param name="moved">What the last refresh moved in the pin, or null when the map is not that refresh's.</param>
    /// <returns>The line, or null when nothing moved.</returns>
    internal static string? Line(PinMovedView? moved) => throw new NotSupportedException();
}
