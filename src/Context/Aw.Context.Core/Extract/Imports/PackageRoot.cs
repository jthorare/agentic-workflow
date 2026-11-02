namespace Aw.Context.Core.Extract.Imports;

/// <summary>A workspace package: its name, its directory, and the entry files its package.json names (repo-relative, in priority order).</summary>
internal sealed record PackageRoot(string Name, string Dir, IReadOnlyList<string> Entries);
