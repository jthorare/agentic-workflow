using Aw.Kernel;

namespace Aw.Sindri.Core.Setup;

/// <summary>Writes the default profile file (setup-once spec §5.1) when there is none: 'sindri profile init'. It never replaces a file that exists.</summary>
public interface IProfileInitializer
{
    /// <summary>Creates the live profile file with the defaults and no repos.</summary>
    /// <param name="defaultEffects">The effect kinds the new file allows (<see cref="IShippedEffects"/>).</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The file's path; SND-PROFILE-002 when a profile already exists there, or the file can't be written (with the OS reason).</returns>
    public ValueTask<Result<string>> Create(IReadOnlyCollection<string> defaultEffects, CancellationToken cancellationToken);
}
