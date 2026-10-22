namespace Aw.Sindri.Core.Onyx;

/// <summary>
/// What carries the login from the endpoint that read it to the service that runs behind the command chain (setup-once spec §5.6): one per request, so the command record carries only the job id.
/// The service takes it only after the chain has authorized and validated the call.
/// </summary>
public interface IOnyxLoginHand
{
    /// <summary>Hands the login over for this request.</summary>
    /// <param name="login">The login the form sent.</param>
    public void Give(OnyxLogin login);

    /// <summary>Takes the login out (it is gone after this).</summary>
    /// <returns>The login, or null when none was given.</returns>
    public OnyxLogin? Take();
}
