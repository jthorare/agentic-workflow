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
