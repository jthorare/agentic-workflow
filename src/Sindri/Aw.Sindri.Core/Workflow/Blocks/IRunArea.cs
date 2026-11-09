using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>
/// An instance's run area, $AW_SINDRI_HOME/workflow/runs/&lt;WFI-id&gt;/ (scoping spec §8.3): the files a Step keeps for the instance beside its facts, such as the
/// scoping station's rounds.jsonl and its candidate packet. A Step names the instance by its address ('T-1/scoping/1', Address) and a file by a
/// bounded relative name (<see cref="RunAreaNames"/>); a write that cannot be made is an error, never a dropped line.
/// </summary>
public interface IRunArea
{
    /// <summary>Writes a file in the instance's run area, creating its directories.</summary>
    /// <param name="address">The instance's address.</param>
    /// <param name="name">The file's relative name.</param>
    /// <param name="content">The text.</param>
    /// <param name="append">True to add the text to the end of the file; false to replace the file.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when written; an error when the address or the name is invalid or the run area cannot be written.</returns>
    public ValueTask<Result<bool>> Write(string address, string name, string content, bool append, CancellationToken cancellationToken);

    /// <summary>Reads a file of the instance's run area.</summary>
    /// <param name="address">The instance's address.</param>
    /// <param name="name">The file's relative name.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text, empty when the file was never written; an error when the address or the name is invalid or the file cannot be read.</returns>
    public ValueTask<Result<string>> Read(string address, string name, CancellationToken cancellationToken);

    /// <summary>The directory of a name in the instance's run area, for a tool that works on a directory (aw-packets freezes the proof pack in place). Nothing is created or read.</summary>
    /// <param name="address">The instance's address.</param>
    /// <param name="name">The relative name of the file or directory.</param>
    /// <returns>The absolute path; an error when the address or the name is invalid.</returns>
    public Result<string> PathFor(string address, string name);
}
