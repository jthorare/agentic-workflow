namespace Aw.Context.Core.Engine;

/// <summary>A checked text question: its search terms.</summary>
/// <param name="Terms">The search terms (letters and digits, lower-case, at most <see cref="QueryRules.MaxTerms"/>).</param>
public sealed record ValidQuery(IReadOnlyList<string> Terms);
