namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The state choice of the Context page, as one pure function.</summary>
internal static class ContextStates
{
    /// <summary>The overlay for what is known: a running build wins over a missing map, because the build is what fills the gap.</summary>
    /// <param name="noMap">Whether the last read was answered "no map yet".</param>
    /// <param name="building">Whether a map build is running.</param>
    /// <returns>The overlay.</returns>
    internal static ContextOverlay Overlay(bool noMap, bool building) => building ? ContextOverlay.Building : noMap ? ContextOverlay.Empty : ContextOverlay.None;
}
