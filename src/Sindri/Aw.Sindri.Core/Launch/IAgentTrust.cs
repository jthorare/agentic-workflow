using Aw.Kernel;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// What an agent CLI must be told before it will work unattended in a directory it has not seen (Claude Code asks "is this a project
/// you trust?" and waits for a person; a launched Step has none). A port: the claude-code adapter edits that CLI's own config file.
/// </summary>
public interface IAgentTrust
{
    /// <summary>Marks <paramref name="directory"/> trusted for the launched user. Idempotent: a directory already trusted writes nothing.</summary>
    /// <param name="directory">The absolute path of the Step's worktree.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or an error (SND-LAUNCH-024) when the config can't be read, parsed or written; the file is left as it was.</returns>
    public ValueTask<Result<Unit>> Trust(string directory, CancellationToken cancellationToken);

    /// <summary>Removes the trust entry of <paramref name="directory"/> (its worktree is gone). Idempotent: no entry is a success.</summary>
    /// <param name="directory">The absolute path of the removed worktree.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or an error (SND-LAUNCH-024) when the config can't be read, parsed or written.</returns>
    public ValueTask<Result<Unit>> Untrust(string directory, CancellationToken cancellationToken);
}
