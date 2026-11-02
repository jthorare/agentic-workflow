using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Features.CheckSandbox;

/// <summary>
/// The form check (sandbox spec §6): the agent Steps run (the approved profile's 'workflow.agent.target', when it is installed and approved for Steps) is -015 ('form-none' or
/// 'form-reserved') when its policy form can't carry a Step's rules. Any other installed agent is no blocker: nothing would park on it. With no target (or an unreadable profile)
/// Steps go to the desk, so there is nothing to check.
/// </summary>
/// <param name="setup">The catalog and what is installed.</param>
/// <param name="catalog">The entries' policy forms.</param>
/// <param name="approved">The approved profile, which names the agent Steps run.</param>
internal sealed class SandboxFormCheck(ILaunchSetup setup, IAgentCatalog catalog, IApprovedProfile approved) : ISandboxFormCheck
{
    private static readonly SandboxCode _unconfinable = SandboxCodes.All.First(static c => ReferenceEquals(c.Error, SindriErrors.LaunchUnconfinable));

    public async ValueTask<IReadOnlyList<SandboxBlocker>> Check(CancellationToken cancellationToken)
    {
        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError || latest.Value?.Profile.Workflow?.Agent?.Target is not { } stepAgent)
        {
            return [];
        }

        var shown = await setup.Show(only: null, cancellationToken).ConfigureAwait(false);
        if (shown.IsError)
        {
            return [];
        }

        return [.. shown.Value.Entries.Where(e => e.Id == stepAgent && e.ResolvedBin is not null && e.StepOk).Select(e => Blocker(e.Id)).OfType<SandboxBlocker>()];
    }

    private SandboxBlocker? Blocker(AgentId target)
    {
        var reason = catalog.Resolve(target)?.PolicyForm switch
        {
            PolicyForm.None => SandboxReasons.FormNone,
            PolicyForm.CursorConfig => SandboxReasons.FormReserved,
            PolicyForm.ClaudeSettingsFile or PolicyForm.CodexOverrides or null => null,
            _ => null,
        };
        return reason is null ? null : new SandboxBlocker(_unconfinable, reason, Source: null, new Dictionary<string, string>(StringComparer.Ordinal) { ["target"] = target.Value });
    }
}
