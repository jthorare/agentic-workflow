using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.CompleteStep;

/// <summary>
/// Who may report an agent run (P8 D6, plan Q6; API spec §4.2, §8.1): only the session the launcher started for it, proven by its launch secret, and only
/// while the run isn't taken over. While it is taken over only the human may, via the desk, whose report the service never reads the view for. A secret on
/// a report for a run that no agent runs is refused too. The secret is checked, never stored, logged or echoed.
/// </summary>
internal static class AgentReportAuthority
{
    /// <summary>Decides a non-human caller's report.</summary>
    /// <param name="view">The instance as read.</param>
    /// <param name="request">The report.</param>
    /// <param name="files">The launches' stored secrets.</param>
    /// <returns>Success with the runtime the report is recorded as (agent for a proven session, desk otherwise), or the refusal: SND-AUTH-002 for a session that isn't the launched one or a taken-over run, SND-AUTH-003 for a secret that doesn't match.</returns>
    internal static Result<StepRuntime> Decide(InstanceView view, CompleteStepRequest request, ILaunchFiles files)
    {
        if (view.AgentRun is not { } agent || agent.Token != request.Run)
        {
            return request.LaunchSecret is null
                ? StepRuntime.Desk
                : SindriErrors.AuthRefused.With($"run {request.Run.Value} is not an agent run, so a launch secret has nothing to prove (fix: report a desk run from a terminal or the Sindri app)");
        }

        if (string.Equals(view.State, EngineStates.TakenOver, StringComparison.Ordinal))
        {
            return SindriErrors.AuthRefused.With($"{view.Address.Value} is taken over: only the human reports its run (fix: run 'sindri workflow complete' in a terminal or the Sindri app, or hand it back)");
        }

        if (request.LaunchSecret is not { } secret)
        {
            return SindriErrors.AuthRefused.With($"run {request.Run.Value} is an agent run: only the session launched for it reports it, with its launch secret");
        }

        if (agent.Launch is null || !LaunchId.TryFrom(agent.Launch, out var launch))
        {
            return SindriErrors.AuthLaunchSecret.With("the run has no launch the secret could belong to");
        }

        var proven = files.Verify(launch, secret);
        return proven.IsError ? proven.Error : StepRuntime.Agent;
    }
}
