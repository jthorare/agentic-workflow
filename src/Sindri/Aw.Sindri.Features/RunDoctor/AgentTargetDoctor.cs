using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.RunDoctor;

/// <summary>The 'agent:target' row: warns when the approved profile's 'workflow.agent.target' can't take a Step's policy. Adds no row otherwise.</summary>
internal sealed class AgentTargetDoctor(IApprovedProfile approved, IAgentCatalog catalog) : IDoctorChecks
{
    public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        return latest is { IsSuccess: true, Value: { } loaded } && AgentTargetWarning.For(loaded.Profile.Workflow?.Agent?.Target, catalog) is { } line
            ? [new DoctorCheck(AgentTargetWarning.Name, CheckState.Warn, line, "set workflow.agent.target to an agent whose policy form is supported, then sindri profile approve")]
            : [];
    }
}
