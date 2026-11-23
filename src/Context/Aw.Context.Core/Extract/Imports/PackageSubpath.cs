namespace Aw.Context.Core.Extract.Imports;

/// <summary>
/// One subpath key of a package.json 'exports' map ('./sql', './*'), split at its '*' like <see cref="PathAlias"/>, with its targets already joined onto the
/// package directory (each may hold the '*'), code targets before type declarations. Blocked is true for a null target ('"./internal/*": null', or conditions
/// that are all null): Node refuses that subpath, so it resolves to nothing, no folder guess (issue 4043).
/// </summary>
internal sealed record PackageSubpath(string Prefix, string Suffix, bool Wild, IReadOnlyList<string> Targets, bool Blocked = false);
