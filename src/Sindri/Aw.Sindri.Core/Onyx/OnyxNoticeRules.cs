namespace Aw.Sindri.Core.Onyx;

/// <summary>When the managed Onyx needs the human (spec §4.2, O6): pure, so the needs-you list and its tests agree.</summary>
public static class OnyxNoticeRules
{
    /// <summary>The command that sets Onyx up or brings it back.</summary>
    public const string Command = "sindri onyx up";

    /// <summary>The notice the app's setup job asks for (setup-once spec §5.6): it waits for the owner's login (the primary action is ConnectOnyx), or it failed (SetupOnyx again). Working shows as the step's progress, not a notice.</summary>
    /// <param name="job">Where the newest setup job stands.</param>
    /// <param name="signupUrl">The sign-up page, said in the waiting notice.</param>
    /// <returns>The notice, or null when the job asks nothing.</returns>
    public static OnyxNotice? ForSetupJob(OnyxSetupJobFact job, string signupUrl)
    {
        ArgumentNullException.ThrowIfNull(job);
        return job switch
        {
            { Phase: OnyxJobPhase.WaitingForLogin } => new OnyxNotice("Connect Onyx", "Create your Onyx account in the browser at " + signupUrl + ", then enter your login in Sindri", Command, "ConnectOnyx"),
            { Phase: OnyxJobPhase.Failed, FailureSentence: { } sentence } => new OnyxNotice("Onyx setup didn't finish", sentence, Command, "SetupOnyx"),
            _ => null,
        };
    }

    /// <summary>Decides whether there is something to show.</summary>
    /// <param name="managed">The approved profile names a managed Onyx.</param>
    /// <param name="setUp">`onyx up` finished its start step on this box.</param>
    /// <param name="declined">The human said no to the first-run offer.</param>
    /// <param name="configured">The approved profile names an Onyx of any kind (a declined offer no longer applies then).</param>
    /// <param name="ensure">What `serve` noted last about starting Onyx (`running`, `needs-setup`, `failed: ...`), or null.</param>
    /// <returns>The notice, or null when nothing needs the human.</returns>
    public static OnyxNotice? Decide(bool managed, bool setUp, bool declined, string? ensure, bool configured = false)
    {
        if (!setUp)
        {
            return managed || (declined && !configured)
                ? new OnyxNotice("Set up Onyx", "Onyx (Sindri's business-context search) isn't set up on this box; `sindri onyx up` does it", Command, "SetupOnyx")
                : null;
        }

        return managed && ensure is { } note && note.StartsWith("failed", StringComparison.Ordinal)
            ? new OnyxNotice("Onyx didn't start", "serve could not bring the managed Onyx up (" + note + "); `sindri onyx status` says why", Command, "SetupOnyx")
            : null;
    }
}
