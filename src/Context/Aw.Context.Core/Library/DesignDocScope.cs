namespace Aw.Context.Core.Library;

/// <summary>The errors decided against a scope's other docs, not in one doc alone: a shared doc id (012), a ref to an item the scope lacks (015), an unknown parent or supersedes (016).</summary>
public static class DesignDocScope
{
    /// <summary>Checks a set of parsed docs.</summary>
    /// <param name="docs">The docs, each with its scope; docs in different scopes never share ids.</param>
    /// <returns>The findings, by path then line.</returns>
    public static IReadOnlyList<DesignDocFinding> Check(IReadOnlyList<ScopedDesignDoc> docs) =>
        throw new NotSupportedException("red: DesignDocScope.Check");
}
