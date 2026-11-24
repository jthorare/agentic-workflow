using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.GetStatus;

/// <summary>
/// What the System page's Agents card and the health make of the collector's problems (#4012). Every row but a test override (SND-SESSION-003) is
/// counted and listed. Two kinds are health problems as well, through the same rows a flagged card uses (#4010), so the banner never says "Everything
/// is running." beside a broken collector: a source that couldn't be read, and a collector that can't run (SND-SESSION-002). Two counted rows are not:
/// a collector the owner turned off in launcher.json (the owner's choice), and a pass that overran its budget (SessionProblem.IsOverrun, set only on
/// the collector's overrun row): slow is not broken, and nothing failed, though that row shares SND-SESSION-001 with a failed source.
/// </summary>
internal static class SessionHealth
{
    /// <summary>The problems the Agents card counts and lists.</summary>
    /// <param name="problems">The last pass's problem rows.</param>
    /// <returns>Every row but a test override.</returns>
    internal static IReadOnlyList<SessionProblem> Counted(IReadOnlyList<SessionProblem> problems) =>
        [.. problems.Where(static p => !IsCode(p, SindriErrors.SessionsTestOverride))];

    /// <summary>The health problems among them: each source that couldn't be read and a collector that can't run; never an overrun or a turned-off collector.</summary>
    /// <param name="problems">The last pass's problem rows.</param>
    /// <returns>One health problem per such row, with its code and sentence.</returns>
    internal static IReadOnlyList<HealthProblem> Problems(IReadOnlyList<SessionProblem> problems) =>
        [.. problems.Where(IsHealthProblem).Select(static p => new HealthProblem(p.Code, p.Text, Clause(p)))];

    private static bool IsHealthProblem(SessionProblem problem) => IsUnreadSource(problem) || IsCollectorDown(problem);

    // SND-SESSION-001 is shared: a source that couldn't be read is a health problem, the overrun row is not.
    private static bool IsUnreadSource(SessionProblem problem) => IsCode(problem, SindriErrors.SessionSourceFailed) && !problem.IsOverrun;

    private static bool IsCollectorDown(SessionProblem problem) => IsCode(problem, SindriErrors.SessionsUnavailable) && problem.Reason != UnavailableReason.Disabled;

    // Only health problems get a clause: an unread source, else a collector that can't run.
    private static string Clause(SessionProblem problem) => IsUnreadSource(problem) ? "an agent session source failed" : "Sindri can't see agent sessions";

    private static bool IsCode(SessionProblem problem, ErrorDefinition definition) => string.Equals(problem.Code, definition.Code, StringComparison.Ordinal);
}
