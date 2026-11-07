namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>One change on the owner's issues since the last dismissed marker.</summary>
/// <param name="Kind">assigned, mentioned or state.</param>
/// <param name="Key">The issue key.</param>
/// <param name="Title">The issue title.</param>
/// <param name="Actor">Who did it; null when Sindri can't know (an assignment or state change it found by comparing two syncs).</param>
/// <param name="At">When.</param>
/// <param name="From">The old state name for a state change; null otherwise.</param>
/// <param name="To">The new state name for a state change; null otherwise.</param>
internal sealed record LinearSinceItem(string Kind, string Key, string Title, LinearPerson? Actor, DateTimeOffset At, string? From, string? To);
