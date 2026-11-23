namespace Aw.Sindri.Features.ScanRepos;

/// <summary>The last line of the scan (first-run spec §3.3): '{done: true, folders: 41, repos: 6}'.</summary>
/// <param name="Done">Always true: the walk ended (it reached its depth or the cap of 500 repos).</param>
/// <param name="Folders">How many folders the walk looked at.</param>
/// <param name="Repos">How many repos it listed.</param>
/// <param name="KnownFailed">True when the GitHub read behind 'known' failed, so no row is ticked: the app says "Couldn't read your GitHub repos. Retry" (spec gap 7).</param>
internal sealed record RepoScanEnd(bool Done, int Folders, int Repos, bool KnownFailed);
