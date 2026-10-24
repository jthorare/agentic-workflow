namespace Aw.Judge.Core.Ports;

/// <summary>The files trait-check reads besides the traits: the process's working directory and the repo's text files.</summary>
public interface ITraitFiles
{
    /// <summary>The directory the command runs in; relative paths and the repo are found from here.</summary>
    public string WorkingDirectory { get; }

    /// <summary>Reads a text file.</summary>
    /// <param name="path">The file's path.</param>
    /// <returns>The text, or null when the file is missing or can't be read, or is binary (holds a NUL).</returns>
    public string? ReadText(string path);
}
