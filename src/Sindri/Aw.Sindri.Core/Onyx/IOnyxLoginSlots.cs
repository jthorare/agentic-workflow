using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Onyx;

/// <summary>
/// The single-use, in-memory place a login waits for the job that will use it (setup-once spec §5.6): keyed by the job id, so the command that wakes the job carries only that id and the password is not a field of
/// any record that is audited, stored or sent. Nothing here is written anywhere; a process restart empties it.
/// </summary>
public interface IOnyxLoginSlots
{
    /// <summary>Puts <paramref name="login"/> in the slot of <paramref name="job"/>, replacing one that was there.</summary>
    /// <param name="job">The job that will use it.</param>
    /// <param name="login">The login.</param>
    public void Put(JobId job, OnyxLogin login);

    /// <summary>Takes the login out of the slot (it is gone after this: single use).</summary>
    /// <param name="job">The job.</param>
    /// <returns>The login, or null when the slot is empty.</returns>
    public OnyxLogin? Take(JobId job);

    /// <summary>Empties the slot (on use, cancel, failure, restart or a refused connect).</summary>
    /// <param name="job">The job.</param>
    public void Clear(JobId job);
}
