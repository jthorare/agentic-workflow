using Aw.Kernel;

namespace Aw.Judge.Core.Ports;

/// <summary>
/// The files 'judge eval' reads and writes: the --record / --replay file and the reports under judge/evals/. Written files are
/// readable by their owner only where the OS has file modes. Every call returns an error, never throws, when the file can't be used.
/// </summary>
public interface IEvalFiles
{
    /// <summary>Appends one line to a file, creating it (and its directory) when missing.</summary>
    /// <param name="path">The file.</param>
    /// <param name="line">The line, without its line break.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or why the file can't be written.</returns>
    public ValueTask<Result<Unit>> AppendLine(string path, string line, CancellationToken cancellationToken);

    /// <summary>Reads a file's non-blank lines.</summary>
    /// <param name="path">The file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The lines in order, or why the file can't be read.</returns>
    public ValueTask<Result<IReadOnlyList<string>>> ReadLines(string path, CancellationToken cancellationToken);

    /// <summary>Writes (replacing) a report file under judge/evals/.</summary>
    /// <param name="name">The file name, with no directory.</param>
    /// <param name="content">The text.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The path written, or why the file can't be written.</returns>
    public ValueTask<Result<string>> WriteReport(string name, string content, CancellationToken cancellationToken);
}
