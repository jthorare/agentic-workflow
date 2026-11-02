namespace Aw.Sindri.Core.Profile;

/// <summary>One key path that differs between two profiles (<see cref="ProfileDiff.Changes"/>).</summary>
/// <param name="Key">The key path, e.g. 'index.pages.schedule-week'.</param>
/// <param name="Before">The value it had, or null when the first profile doesn't set it.</param>
/// <param name="After">The value it has, or null when the second profile doesn't set it.</param>
public sealed record ProfileChange(string Key, string? Before, string? After);
