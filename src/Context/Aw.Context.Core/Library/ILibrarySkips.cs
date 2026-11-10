namespace Aw.Context.Core.Library;

/// <summary>Files the library feed leaves out of a root although they are markdown there (the host knows them, Context doesn't): the Obsidian todo note, which Sindri syncs itself and the library must not read as a design doc.</summary>
public interface ILibrarySkips
{
    /// <summary>The files to skip in one root.</summary>
    /// <param name="root">The root's name (the vault's name).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Their paths relative to the root, with '/' separators; none when the root holds no such file.</returns>
    public ValueTask<IReadOnlyList<string>> In(string root, CancellationToken cancellationToken);
}
