using Aw.Kernel;

namespace Aw.Meta.Core.Ports;

/// <summary>Reads gate step logs.</summary>
public interface ILogFiles
{
    /// <summary>Reads the end of a log file.</summary>
    /// <param name="path">The file.</param>
    /// <param name="maxCharacters">How many characters to keep, counted from the end.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The text, or AW-META-013 when the file can't be read.</returns>
    public ValueTask<Result<string>> Tail(string path, int maxCharacters, CancellationToken cancellationToken);

    /// <summary>Finds the highest run directory of a pull request's gate directory.</summary>
    /// <param name="gateDirectory">The pull request's gates/&lt;pr&gt; directory.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The highest all-digit subdirectory name (other names such as tree are ignored), 0 when the directory is missing or holds none, AW-META-013 when it can't be read.</returns>
    public ValueTask<Result<int>> LatestRun(string gateDirectory, CancellationToken cancellationToken);
}
