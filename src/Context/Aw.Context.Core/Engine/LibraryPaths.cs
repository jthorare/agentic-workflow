using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>The library's path rules (review page spec §5.1), which need the machine's home folders: '~/' expansion, the refused roots and roots inside one another. A path that does not exist is not refused here.</summary>
public static class LibraryPaths
{
    /// <summary>Expands and checks the roots of <paramref name="source"/>.</summary>
    /// <param name="source">The checked settings.</param>
    /// <param name="host">The machine's home folders.</param>
    /// <returns>The roots (the 'aw' root first, unless turned off), or SND-LIB-001 naming the key path.</returns>
    public static Result<IReadOnlyList<LibraryRoot>> Resolve(LibrarySource source, LibraryHost host) =>
        throw new NotSupportedException();
}
