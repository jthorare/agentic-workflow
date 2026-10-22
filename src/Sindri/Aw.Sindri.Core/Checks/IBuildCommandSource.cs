using Aw.Kernel;

namespace Aw.Sindri.Core.Checks;

/// <summary>Where the build and ship stations get the repo's commands (line spec §7.2, LN-WF9).</summary>
public interface IBuildCommandSource
{
    /// <summary>Reads the commands.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The commands, none when none are configured; an error when the profile can't be read or a command line can't be split.</returns>
    public ValueTask<Result<CommandSet>> Read(CancellationToken cancellationToken);
}
