namespace Aw.Sindri.Features.ScanRepos;

/// <summary>One line of the scan: a main checkout the walk found (first-run spec §3.3).</summary>
/// <param name="Path">The checkout's real path.</param>
/// <param name="Host">The origin's host ('github.com'), or null when it has no origin.</param>
/// <param name="Slug">The origin's 'owner/name', or null when it has no origin.</param>
/// <param name="Known">Whether the owner worked on this repo in GitHub lately; the app ticks these.</param>
/// <param name="InProfile">Whether the live profile already names this folder.</param>
/// <param name="Worktrees">How many linked worktrees fold into this checkout.</param>
internal sealed record RepoScanRow(string Path, string? Host, string? Slug, bool Known, bool InProfile, int Worktrees);
