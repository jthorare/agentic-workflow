namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>A place focus is sent to: the region, and which one inside it.</summary>
/// <param name="Region">The region.</param>
/// <param name="Key">A panel id, a diff line "path:line", a group name; empty for the strip.</param>
internal sealed record ReviewFocus(ReviewFocusRegion Region, string Key = "")
{
    /// <summary>No focus change.</summary>
    internal static ReviewFocus None { get; } = new(ReviewFocusRegion.None);
}
