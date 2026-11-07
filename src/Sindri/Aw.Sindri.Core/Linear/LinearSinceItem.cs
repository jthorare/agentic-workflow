
namespace Aw.Sindri.Core.Linear;

/// <summary>One change on the owner's issues since a marker.</summary>
/// <param name="Kind">assigned, mentioned or state.</param>
/// <param name="Key">The issue's key, 'ENG-12'.</param>
/// <param name="Title">The issue's title.</param>
/// <param name="Actor">Who did it; null when the store doesn't say.</param>
/// <param name="At">When.</param>
/// <param name="From">The old state's name for a state change; null otherwise.</param>
/// <param name="To">The new state's name for a state change; null otherwise.</param>
public sealed record LinearSinceItem(string Kind, string Key, string Title, LinearPerson? Actor, DateTimeOffset At, string? From, string? To);
