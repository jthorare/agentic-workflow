using System.Globalization;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// The park for a Step whose dependency is down (owner decision q90, q93): a failure no transition takes is retried
/// <see cref="MaxRetries"/> times, then the Step parks as 'unavailable' with a rerun to resume it. It is never a question for a human, so it stays
/// out of the needs-you list, the desk and the counts, and shows in health instead.
/// </summary>
internal static class Unavailables
{
    /// <summary>How many times the engine runs a failed Step again before it parks: three tries in all, as many as a launch gets.</summary>
    public const int MaxRetries = 2;

    /// <summary>What the park says: the dependency, how often it was retried and the last error.</summary>
    /// <param name="dependency">The dependency that was down (an input's block id, the launcher, the Step itself).</param>
    /// <param name="retries">How many times the Step was run again.</param>
    /// <param name="error">The last failure, as the human reads it.</param>
    /// <returns>The sentence, without a full stop.</returns>
    public static string Question(string dependency, int retries, string error) =>
        string.Create(CultureInfo.InvariantCulture, $"{dependency} is unavailable after {retries} retries; the last error: {error}");

    /// <summary>Whether a failure is an agent session that is gone: it ended as 'exit' or 'lost' (a launch failure, an 'idle' stop or a block failure is not).</summary>
    /// <param name="failed">The failure.</param>
    /// <returns>True when the agent's session ended without a report (a launch failure names its dependency, the launcher, so it is never one).</returns>
    public static bool IsGoneSession(StepFailed failed) =>
        failed.Actor.StartsWith("service:agent:", StringComparison.Ordinal) && failed.Dependency is null && (string.Equals(failed.Reason, "exit", StringComparison.Ordinal) || string.Equals(failed.Reason, "lost", StringComparison.Ordinal));

    /// <summary>What the park says for a gone agent session: the Step, how often its session was relaunched and the last error.</summary>
    /// <param name="step">The Step whose session is gone.</param>
    /// <param name="reason">How the session ended ('exit' or 'lost').</param>
    /// <param name="retries">How many times the session was relaunched.</param>
    /// <param name="error">The last failure, as the human reads it.</param>
    /// <returns>The sentence, without a full stop.</returns>
    public static string Gone(string step, string reason, int retries, string error) =>
        string.Create(CultureInfo.InvariantCulture, $"{step}'s agent session is gone ({reason}), relaunched {retries} times; the last error: {error}");

    /// <summary>The words an idle-agent park says after the Step's name; <see cref="IsIdleQuestion"/> finds a park by them.</summary>
    private const string IdleWords = "'s agent session went idle with no report (";

    /// <summary>Whether a failure is an agent session that sat idle and was ended with no report (reason 'idle', the idle sweep's stop).</summary>
    /// <param name="failed">The failure.</param>
    /// <returns>True for an agent Step failure with reason 'idle' (a launch failure names its dependency, so it is never one).</returns>
    public static bool IsIdleSession(StepFailed failed) =>
        failed.Actor.StartsWith("service:agent:", StringComparison.Ordinal) && failed.Dependency is null && string.Equals(failed.Reason, "idle", StringComparison.Ordinal);

    /// <summary>What the park says for an agent session that went idle: the Step, why the sweep ended it, how often it was relaunched and the last error.</summary>
    /// <param name="step">The Step whose session went idle.</param>
    /// <param name="cause">The failure detail's first word ('agent-stalled'), or the reason when it has none.</param>
    /// <param name="retries">How many times the session was relaunched.</param>
    /// <param name="error">The last failure, as the human reads it.</param>
    /// <returns>The sentence, without a full stop.</returns>
    public static string Idle(string step, string cause, int retries, string error) =>
        string.Create(CultureInfo.InvariantCulture, $"{step}{IdleWords}{cause}), relaunched {retries} times; the last error: {error}");

    /// <summary>The first word of a failure's detail ('agent-stalled' of 'agent-stalled: the agent sat idle ...'), else its reason.</summary>
    /// <param name="failed">The failure.</param>
    /// <returns>The cause word.</returns>
    public static string CauseOf(StepFailed failed) =>
        failed.Detail is { Length: > 0 } detail ? detail.Split(':', ' ')[0] : failed.Reason;

    /// <summary>Whether a park's question is <see cref="Idle"/>'s sentence (the view carries the question, not the failure).</summary>
    /// <param name="question">The park's question.</param>
    /// <returns>True for an idle-agent park.</returns>
    public static bool IsIdleQuestion(string question) => question.Contains(IdleWords, StringComparison.Ordinal);

    /// <summary>The park.</summary>
    /// <param name="question">What it says (<see cref="Question"/>).</param>
    /// <param name="actor">Who reported the failure.</param>
    /// <param name="code">The dependency's own error code, where there is one.</param>
    /// <returns>A step park whose answer runs the Step again.</returns>
    public static Suspension Park(string question, string actor, string? code) => new(
        SuspensionKind.Park, ParkReason.Unavailable, ParkKind.Step, Escalation: null, question, actor, Mirrors: null,
        new ResumeAction(ResumeKind.Rerun, Withheld: null, Gate: null, Effects: []))
    {
        Code = code,
    };
}
