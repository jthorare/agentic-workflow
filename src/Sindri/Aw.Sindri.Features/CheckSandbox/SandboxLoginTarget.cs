using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.CheckSandbox;

/// <summary>The agent Steps run on this box and the profile's sandbox section: shared by the login probe and the fixture login.</summary>
internal static class SandboxLoginTarget
{
    /// <summary>A floor for how long the login must stay valid: a Step's own wall time is only known at launch.</summary>
    internal static readonly TimeSpan Needed = TimeSpan.FromMinutes(30);

    /// <summary>The approved profile's 'workflow.agent.target' when it is installed, approved for Steps and its policy form is expressible; null otherwise.</summary>
    internal static async ValueTask<(AgentId Target, SandboxSettings? Settings)?> Find(ILaunchSetup setup, IAgentCatalog catalog, IApprovedProfile approved, CancellationToken cancellationToken)
    {
        var profile = await approved.Latest(cancellationToken).ConfigureAwait(false);
        var loaded = profile.IsError ? null : profile.Value;
        if (loaded?.Profile.Workflow?.Agent?.Target is not { } agent)
        {
            return null;
        }

        var shown = await setup.Show(only: null, cancellationToken).ConfigureAwait(false);
        return Runnable(shown, agent) && catalog.Resolve(agent)?.PolicyForm is PolicyForm.ClaudeSettingsFile or PolicyForm.CodexOverrides ? (agent, loaded.Profile.Sandbox) : null;
    }

    private static bool Runnable(Result<LaunchShowReport> shown, AgentId agent) => shown.IsSuccess && shown.Value.Entries.Any(e => e.Id == agent && e.ResolvedBin is not null && e.StepOk);
}
