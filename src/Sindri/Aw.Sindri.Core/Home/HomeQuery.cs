namespace Aw.Sindri.Core.Home;

/// <summary>A page of Home rows to read (home spec §8).</summary>
/// <param name="Group">One group, or null for both.</param>
/// <param name="States">The states to include.</param>
/// <param name="After">The id of the last row of the previous page, or null.</param>
/// <param name="Limit">The most rows to return.</param>
public sealed record HomeQuery(HomeGroup? Group, IReadOnlyList<HomeState> States, string? After, int Limit);
