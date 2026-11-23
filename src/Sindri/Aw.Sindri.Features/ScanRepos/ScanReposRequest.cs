using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ScanRepos;

/// <summary>Look for repos under a folder (GET /sindri/setup/repos?root=~&amp;depth=3, first-run spec §3.3). Read-only.</summary>
/// <param name="Root">The folder to walk: the home folder ('~' or null), '~/…', or an absolute path under the home folder.</param>
/// <param name="Depth">How many folders deep the walk goes below the root; null is <see cref="Core.Setup.RepoScanRules.DefaultDepth"/>.</param>
internal sealed record ScanReposRequest(string? Root, int? Depth) : IQuery<ScanReposResponse>;
