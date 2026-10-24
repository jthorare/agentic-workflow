using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Facade;

/// <summary>A symbol search (spec §7): exact or prefix, never a wildcard.</summary>
/// <param name="Text">An exact name, 'path', 'path#name' or a ref; or a prefix.</param>
/// <param name="Match">Exact or prefix.</param>
/// <param name="Repo">One repo, or null for every repo in scope.</param>
/// <param name="Limit">At most this many hits (any number from 1; the default is <see cref="CodeIndexRules.DefaultSymbolHits"/>.</param>
public sealed record SymbolQuery(string Text, SymbolMatch Match, RepoName? Repo, int Limit = CodeIndexRules.DefaultSymbolHits);
