using Aw.Kernel;

namespace Aw.Sindri.Core.Leftovers;

/// <summary>The disk access the Onyx leftover listing and purge need, by full path.</summary>
public interface ILeftoverFiles
{
    /// <summary>Reads a text file.</summary>
    /// <param name="path">The file.</param>
    /// <returns>Its text; null when it is missing or unreadable.</returns>
    public string? ReadText(string path);

    /// <summary>Tells whether a directory exists.</summary>
    /// <param name="path">The directory.</param>
    /// <returns>True when it does.</returns>
    public bool DirectoryExists(string path);

    /// <summary>Deletes a directory and everything in it.</summary>
    /// <param name="path">The directory.</param>
    /// <returns>Ok, or SND-INDEX-014 naming the fix.</returns>
    public Result<Unit> DeleteDirectory(string path);
}
