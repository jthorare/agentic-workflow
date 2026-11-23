namespace Aw.Context.Core.Extract.Imports;

/// <summary>
/// One subpath key of a package.json 'exports' map ('./sql', './*'), split at its '*' like <see cref="PathAlias"/>, with its targets already joined onto the
/// package directory (each may hold the '*'), code targets before type declarations.
/// </summary>
internal sealed record PackageSubpath(string Prefix, string Suffix, bool Wild, IReadOnlyList<string> Targets);
