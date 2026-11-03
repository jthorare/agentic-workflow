namespace Aw.Sindri.Core.Judge;

/// <summary>The file system as a 'step.judge.choose' image sees it: whether the file is there, and where it really is.</summary>
public interface IStepImageFiles
{
    /// <summary>The real location of a file, with every symlink followed.</summary>
    /// <param name="path">The absolute path the recipe's fact gave.</param>
    /// <returns>The real path, or null when no file is there.</returns>
    public string? RealPath(string path);

    /// <summary>The real location of a directory, with every symlink followed.</summary>
    /// <param name="directory">The directory.</param>
    /// <returns>The real path; the directory's own full path when it isn't a link or isn't there.</returns>
    public string RealDirectory(string directory);
}
