namespace Aw.Sindri.Core.Home;

/// <summary>One page of Home rows (home spec §8).</summary>
/// <param name="Rows">The rows, in ranked order.</param>
/// <param name="Counts">The counts over the whole view.</param>
/// <param name="Cursor">The id to pass as After for the next page, or null at the end.</param>
public sealed record HomePage(IReadOnlyList<HomeRow> Rows, HomeCounts Counts, string? Cursor);
