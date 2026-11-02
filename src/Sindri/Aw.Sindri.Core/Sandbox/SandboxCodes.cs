namespace Aw.Sindri.Core.Sandbox;

/// <summary>
/// The kind table of the sandbox launch codes (sandbox spec SB11, §6): the one table the launcher and the engine both read, so the engine
/// routes on a code and never on text. The golden 'Golden/Sandbox/code-kinds.json' pins it.
/// </summary>
public static class SandboxCodes
{
    /// <summary>The sandbox launch codes, in number order, with their kinds.</summary>
    public static IReadOnlyList<SandboxCode> All { get; } =
    [
        new(SindriErrors.LaunchSandboxUnavailable, LaunchCodeKind.NoRuntime, HasHttpClass: true),
        new(SindriErrors.LaunchUnconfinable, LaunchCodeKind.LaunchPark, HasHttpClass: true),
        new(SindriErrors.LaunchSelfCheckFailed, LaunchCodeKind.LaunchPark, HasHttpClass: true),
        new(SindriErrors.LaunchLoginUnavailable, LaunchCodeKind.NoRuntime, HasHttpClass: true),
        new(SindriErrors.LaunchGitHubTokenFailed, LaunchCodeKind.StepFailure, HasHttpClass: true, FailureReason: "launch"),
        new(SindriErrors.LaunchLimitHit, LaunchCodeKind.StepFailure, HasHttpClass: false, FailureReason: "limit"),
        new(SindriErrors.LaunchSessionWait, LaunchCodeKind.ResourceWait, HasHttpClass: true),
        new(SindriErrors.LaunchPrepareFailed, LaunchCodeKind.StepFailure, HasHttpClass: true, FailureReason: "launch"),
    ];

    /// <summary>The kind of a sandbox launch code.</summary>
    /// <param name="code">A code such as 'SND-LAUNCH-014'.</param>
    /// <returns>The kind, or null when the code isn't a sandbox launch code.</returns>
    public static LaunchCodeKind? KindOf(string code) =>
        All.FirstOrDefault(c => string.Equals(c.Error.Code, code, StringComparison.Ordinal))?.Kind;
}
