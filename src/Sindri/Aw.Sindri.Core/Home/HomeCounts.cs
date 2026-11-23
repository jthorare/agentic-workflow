namespace Aw.Sindri.Core.Home;

/// <summary>How many rows Home holds per bucket (home spec §8).</summary>
/// <param name="NeedsYou">Open rows in needs you.</param>
/// <param name="Moving">Open rows in moving.</param>
/// <param name="Snoozed">Snoozed rows.</param>
/// <param name="Done">Done rows.</param>
public sealed record HomeCounts(int NeedsYou, int Moving, int Snoozed, int Done);
