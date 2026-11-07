namespace Aw.Context.Core.Engine;

/// <summary>One root the library feed reads, with its path expanded and checked.</summary>
public sealed class LibraryRoot
{
    /// <summary>The root name: 'aw' or a vault's name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The absolute path.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>The root's own deny globs (the built-in ones are the feed's).</summary>
    public IReadOnlyList<string> Deny { get; init; } = [];

    /// <summary>Whether docs of this root may reach a model call.</summary>
    public bool Models { get; init; }

    /// <summary>True for the 'aw' root, which reads only the plan, proof and system-design folders of each repo slug.</summary>
    public bool IsAw { get; init; }
}
