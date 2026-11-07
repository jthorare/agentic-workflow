namespace Aw.Context.Core.Engine;

/// <summary>One 'context.sources.library.vaults' entry, checked (review page spec §5.1): a folder of notes the library reads.</summary>
public sealed class LibraryVault
{
    /// <summary>The root name docs are keyed under ('doc:&lt;name&gt;/&lt;path&gt;'): lowercase letters, digits and '-', 1 to 32, not 'aw', not starting with 'repo'.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The folder as written: absolute, or with a leading '~/' that <see cref="LibraryPaths.Resolve"/> expands.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>The vault's own deny globs, on top of the built-in ones.</summary>
    public IReadOnlyList<string> Deny { get; init; } = [];

    /// <summary>Whether docs of this vault may reach a model call; false by default.</summary>
    public bool Models { get; init; }
}
