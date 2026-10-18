namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What a failed launch does to the run (engine P8 D8).</summary>
public enum AgentLaunchFailureKind
{
    /// <summary>The launcher refused the entry or the Step's form (SND-LAUNCH-012, -013): the instance parks as a spec conflict, with the code.</summary>
    Park = 1,

    /// <summary>The host could not start the session: the Step ends as 'step.failed' with the reason 'launch'.</summary>
    StepFailed = 2,

    /// <summary>The Step has no agent runtime on this box right now (SND-LAUNCH-014, -017): the run closes, and a deskCapable Step runs at the desk with no round counted, any other parks 'SND-WF-010' (sandbox spec SB16, N1).</summary>
    NoRuntime = 3,

    /// <summary>The Step can't be confined (SND-LAUNCH-015) or its self-check failed (-016): the instance parks as 'launch', whose Resume starts a new run.</summary>
    LaunchPark = 4,

    /// <summary>The session cap is reached (SND-LAUNCH-020): a queue condition the launch activity waits out, never a fault.</summary>
    Wait = 5,
}
