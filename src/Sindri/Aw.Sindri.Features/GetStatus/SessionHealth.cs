using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.GetStatus;

/// <summary>
/// What the System page's Agents card and the health make of the collector's problems (#4012). A test override (SND-SESSION-003) is no problem: not
/// counted, not listed. A failed source (SND-SESSION-001) or a collector that can't run (SND-SESSION-002) is a health problem as well, through the same
/// rows a flagged card uses (#4010), so the banner never says "Everything is running." beside a counted problem. A collector the owner turned off in
/// launcher.json is listed but is the owner's choice, not a health problem.
/// </summary>
internal static class SessionHealth
{
    /// <summary>The problems the Agents card counts and lists.</summary>
    /// <param name="problems">The last pass's problem rows.</param>
    /// <returns>Every row but a test override.</returns>
    internal static IReadOnlyList<SessionProblem> Counted(IReadOnlyList<SessionProblem> problems) =>
        [.. problems.Where(static p => !IsCode(p, SindriErrors.SessionsTestOverride))];

    /// <summary>The health problems among them: each failed source and a collector that can't run.</summary>
    /// <param name="problems">The last pass's problem rows.</param>
    /// <returns>One health problem per such row, with its code and sentence.</returns>
    internal static IReadOnlyList<HealthProblem> Problems(IReadOnlyList<SessionProblem> problems) =>
        [.. problems.Where(IsHealthProblem).Select(static p => new HealthProblem(p.Code, p.Text, Clause(p)))];

    private static bool IsHealthProblem(SessionProblem problem) =>
        IsCode(problem, SindriErrors.SessionSourceFailed) || (IsCode(problem, SindriErrors.SessionsUnavailable) && problem.Reason != UnavailableReason.Disabled);

    private static string Clause(SessionProblem problem) =>
        IsCode(problem, SindriErrors.SessionSourceFailed) ? "an agent session source failed" : "Sindri can't see agent sessions";

    private static bool IsCode(SessionProblem problem, ErrorDefinition definition) => string.Equals(problem.Code, definition.Code, StringComparison.Ordinal);
}
