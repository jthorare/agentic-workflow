using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Ids;

namespace Aw.Evidence.Core.Desktop;

/// <summary>A desktop-run's record, written to &lt;run-dir&gt;/summary.json (schema 'ui-evidence.desktop/1'). Every key is always written.</summary>
/// <param name="Schema">Always <see cref="SchemaName"/>.</param>
/// <param name="Kind">Always 'desktop'.</param>
/// <param name="RunId">The run (the run directory's name).</param>
/// <param name="StartedAt">When the run started (UTC).</param>
/// <param name="FinishedAt">When it finished (UTC).</param>
/// <param name="AppBuild">The --app-build commit, when given.</param>
/// <param name="ManifestSha256">SHA-256 of the screens manifest.</param>
/// <param name="Outcome">How the run ended; agrees with the exit code.</param>
/// <param name="Headline">One line, 200 characters at most, scrubbed.</param>
/// <param name="Screens">Every screen at every size, in manifest order.</param>
/// <param name="Refusal">Set when a guard refused the run (exit 1).</param>
/// <param name="Error">Set when the run errored (exit 4).</param>
/// <param name="Notes">How the frames were made that a reader should know: the app's 'display: ...' lines (the user's display or a virtual X server).</param>
public sealed record DesktopSummary(
    string Schema,
    string Kind,
    RunId RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    string? AppBuild,
    string ManifestSha256,
    DesktopOutcome Outcome,
    string Headline,
    IReadOnlyList<DesktopScreenResult> Screens,
    CodedMessage? Refusal,
    CodedMessage? Error,
    IReadOnlyList<string> Notes)
{
    /// <summary>The schema this build writes.</summary>
    public const string SchemaName = "ui-evidence.desktop/1";

    /// <summary>The 'kind' a desktop summary carries.</summary>
    public const string KindName = "desktop";

    /// <summary>The process exit code this summary's outcome stands for.</summary>
    /// <returns>0 passed, 2 failed, 1 refused, 4 errored.</returns>
    public int ExitCode() => Outcome switch
    {
        DesktopOutcome.Passed => 0,
        DesktopOutcome.Failed => 2,
        DesktopOutcome.Refused => 1,
        DesktopOutcome.Errored => 4,
        _ => 4,
    };
}
