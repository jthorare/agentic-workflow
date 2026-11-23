namespace Aw.Context.Core.Extract.Imports;

/// <summary>A workspace package: its name, its directory, the entry files its package.json names (repo-relative, in priority order), and its 'exports' subpaths.</summary>
internal sealed record PackageRoot(string Name, string Dir, IReadOnlyList<string> Entries, IReadOnlyList<PackageSubpath> Subpaths);
