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

    /// <summary>Claims the next run directory of a pull request's gate directory: creates gates/&lt;pr&gt;/&lt;run&gt;/ with an exclusive marker, so two passes planned at once never share a run.</summary>
    /// <param name="gateDirectory">The pull request's gates/&lt;pr&gt; directory.</param>
    /// <param name="recorded">The latest recorded run, 0 when none.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The claimed run: above the recorded run, above every all-digit directory on disk and not claimed by another caller; AW-META-013 when the directory can't be written.</returns>
    public ValueTask<Result<int>> ClaimRun(string gateDirectory, int recorded, CancellationToken cancellationToken);
}
