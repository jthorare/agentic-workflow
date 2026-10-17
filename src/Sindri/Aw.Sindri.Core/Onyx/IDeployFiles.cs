using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>The pinned deployment files of the Onyx release, fetched from the pinned commit and verified against the lock before anything is written.</summary>
public interface IDeployFiles
{
    /// <summary>Makes every pinned file present under a directory with its pinned hash.</summary>
    /// <param name="pins">The lock: the commit and each file with its sha256.</param>
    /// <param name="deployDir">The directory the pinned paths are laid out under.</param>
    /// <param name="progress">Called with a line per file.</param>
    /// <param name="cancellationToken">Cancels the fetch.</param>
    /// <returns>How many files were downloaded and how many were already right; or SND-ONYX-004 for the first file that failed its pin or couldn't be fetched (the other good files are still written, and a failed file leaves what was there untouched).</returns>
    public ValueTask<Result<DeployFetch>> Fetch(OnyxPins pins, string deployDir, Action<string> progress, CancellationToken cancellationToken);
}
