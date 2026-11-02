namespace Aw.Context.Core.Extract.Imports;

/// <summary>A tsconfig 'paths' entry: the pattern split at its '*', and its targets already joined onto the base directory.</summary>
internal sealed record PathAlias(string Scope, string Prefix, string Suffix, bool Wild, IReadOnlyList<string> Targets);
