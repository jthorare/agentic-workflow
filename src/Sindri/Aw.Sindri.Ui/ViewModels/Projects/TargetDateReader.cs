namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>
/// Reads the forms people type in the Add project sheet's Target box ("Dec 5", "next fri", "2026-12-05", "tomorrow") into a date. Pure: today comes in as an argument,
/// so a relative form resolves from the caller's clock.
/// </summary>
internal static class TargetDateReader
{
    /// <summary>Reads the text.</summary>
    /// <param name="text">What is in the box.</param>
    /// <param name="today">Today's date, from the caller's clock.</param>
    /// <returns>Empty for blank text, the date for a readable one, Unreadable otherwise.</returns>
    internal static TargetReading Read(string? text, DateOnly today) => throw new NotSupportedException();
}
