namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>Which control an overlay hands focus back to when it closes: the one that opened it, not the page region that merely held focus.</summary>
internal static class OverlayOpener
{
    /// <summary>Picks the opener.</summary>
    /// <typeparam name="T">The control type.</typeparam>
    /// <param name="focused">What had focus when the overlay opened; null when nothing did.</param>
    /// <param name="page">The page region, which holds focus when a click or an automation invoke leaves it on the page.</param>
    /// <param name="invoker">The control whose command opened the overlay; null when a chord or the palette opened it.</param>
    /// <returns>The invoker when focus was on nothing or on the page region, else what had focus.</returns>
    internal static T? Choose<T>(T? focused, T page, T? invoker)
        where T : class => invoker is not null && (focused is null || ReferenceEquals(focused, page)) ? invoker : focused;
}
