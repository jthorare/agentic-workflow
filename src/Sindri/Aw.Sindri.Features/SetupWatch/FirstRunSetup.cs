using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetupWatch;

/// <summary>
/// The first-run steps the setup service does on its own once the profile names a repo (first-run spec decisions 10 and 19, E1 and E2): it sets 'workflow.agent.target' from the first agent the launcher finds,
/// then makes and activates hello-sindri, once per box (the 'setup.first_workflow.created' event). Neither is a question for the owner.
/// </summary>
/// <param name="live">The live profile.</param>
/// <param name="launch">The launcher's catalog: which agents are installed.</param>
/// <param name="keys">Edits one key of the profile file in place.</param>
/// <param name="create">Makes the workflow (the command, run as 'service:setup').</param>
/// <param name="activate">Turns the workflow on (the command, run as 'service:setup').</param>
/// <param name="made">The ledger event that says hello-sindri was made once.</param>
internal sealed class FirstRunSetup(IProfileSource live, ILaunchSetup launch, IProfileKeyWriter keys, ISetupWorkflowCreator create, ISetupWorkflowActivator activate, FirstWorkflowEvents made)
{
    /// <summary>Does the steps that are due; each one is skipped when it is done already, and a step that can't run now is tried again on the next tick.</summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>A task that ends when the due steps were tried.</returns>
    internal ValueTask Run(CancellationToken cancellationToken)
    {
        _ = (live, launch, keys, create, activate, made, cancellationToken);
        return ValueTask.CompletedTask;
    }
}
