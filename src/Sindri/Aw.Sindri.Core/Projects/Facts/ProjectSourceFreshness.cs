namespace Aw.Sindri.Core.Projects.Facts;

/// <summary>One source's sync state for the freshness line (projects page spec §3.1): 'linear-projects', 'linear', 'github'.</summary>
/// <param name="Source">The source id.</param>
/// <param name="Usable">Whether it has completed a read the page can show.</param>
/// <param name="LastSync">When it last synced, or null.</param>
/// <param name="LastCode">The code of its last failure (such as a missing token), or null when the last run was clean.</param>
public sealed record ProjectSourceFreshness(string Source, bool Usable, DateTimeOffset? LastSync, string? LastCode);
