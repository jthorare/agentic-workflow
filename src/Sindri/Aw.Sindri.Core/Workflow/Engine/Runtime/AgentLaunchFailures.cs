using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>Maps a <see cref="FailedLaunch"/> to what the engine does with the run (engine P8 D8).</summary>
public static class AgentLaunchFailures
{
    /// <summary>SND-LAUNCH-012 and -013 park the instance as a spec conflict; anything else ends the Step as 'step.failed' with the reason 'launch'.</summary>
    /// <param name="failed">The launcher's failure.</param>
    /// <returns>What to do.</returns>
    public static AgentLaunchFailure Of(FailedLaunch failed)
    {
        ArgumentNullException.ThrowIfNull(failed);
        var error = failed.Error;
        if (SandboxCodes.All.FirstOrDefault(c => string.Equals(c.Error.Code, error.Code, StringComparison.Ordinal)) is { } sandbox)
        {
            return new AgentLaunchFailure(KindOf(sandbox.Kind), error.Code, error.Message)
            {
                Reason = SandboxRefusal.ReasonOf(error),   // read here: an error's extensions don't cross Temporal
                Source = SandboxRefusal.SourceTextOf(error),
                FailureReason = sandbox.FailureReason,
            };
        }

        var parks = string.Equals(error.Code, SindriErrors.LaunchNotApproved.Code, StringComparison.Ordinal) || string.Equals(error.Code, SindriErrors.LaunchFormUnsupported.Code, StringComparison.Ordinal);
        return new AgentLaunchFailure(parks ? AgentLaunchFailureKind.Park : AgentLaunchFailureKind.StepFailed, error.Code, error.Message);
    }

    private static AgentLaunchFailureKind KindOf(LaunchCodeKind kind) => kind switch
    {
        LaunchCodeKind.NoRuntime => AgentLaunchFailureKind.NoRuntime,
        LaunchCodeKind.LaunchPark => AgentLaunchFailureKind.LaunchPark,
        LaunchCodeKind.ResourceWait => AgentLaunchFailureKind.Wait,
        LaunchCodeKind.StepFailure or _ => AgentLaunchFailureKind.StepFailed,
    };
}
