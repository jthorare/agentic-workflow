using System.Text.Json.Serialization;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Scripts;
using Aw.Evidence.Core.Usage;
using StepStatus = Aw.Evidence.Core.Steps.StepStatus;

namespace Aw.Evidence.Core.Runs;

/// <summary>A run's record, written to &lt;run-dir&gt;/summary.json (schema 'ui-evidence.run/1'). Paths are absolute.</summary>
/// <param name="Schema">Always <see cref="SchemaName"/>.</param>
/// <param name="RunId">The run (the run directory's name).</param>
/// <param name="RunInstance">This invocation, new every time (a rerun in the same directory gets another).</param>
/// <param name="StartedAt">When the run started (UTC).</param>
/// <param name="FinishedAt">When it finished (UTC).</param>
/// <param name="Route">The script's route.</param>
/// <param name="Role">The script's role.</param>
/// <param name="Host">The origin the run drove.</param>
/// <param name="AppBuild">The --app-build commit, when given.</param>
/// <param name="Fixtures">The --fixtures identity, when given.</param>
/// <param name="ScriptSha256">SHA-256 of the script file, so a frozen check can prove what ran.</param>
/// <param name="Pr">The pull request from the script's planning block.</param>
/// <param name="Viewports">The viewports, in order.</param>
/// <param name="Steps">Every step in every viewport.</param>
/// <param name="Visual">The visual verdict.</param>
/// <param name="LintFindings">Lint findings per viewport.</param>
/// <param name="Invocations">Every model call (or cache hit), for the cost baseline.</param>
/// <param name="Planning">The script's planning block.</param>
/// <param name="Evidence">Traces and videos.</param>
/// <param name="At">The side of the change the --at flag named (base or head), or null when it was not given; recorded only (DTC9).</param>
public sealed record RunSummary(
    string Schema,
    RunId RunId,
    RunInstanceId RunInstance,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    string Route,
    string Role,
    string Host,
    string? AppBuild,
    string? Fixtures,
    string ScriptSha256,
    string? Pr,
    IReadOnlyList<ViewportKind> Viewports,
    IReadOnlyList<RunStep> Steps,
    RunVisual Visual,
    IReadOnlyList<RunLintFinding> LintFindings,
    IReadOnlyList<ModelInvocation> Invocations,
    PlanningMeta? Planning,
    RunEvidence Evidence,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DataRunAt? At = null)
{
    /// <summary>The schema this build writes and reads.</summary>
    public const string SchemaName = "ui-evidence.run/1";

    /// <summary>The file a run writes in its run directory, and publish-plan reads.</summary>
    public const string FileName = "summary.json";

    /// <summary>True when every step passed.</summary>
    /// <returns>Whether the run's checks passed.</returns>
    public bool AllStepsPassed() => Steps.All(static s => s.Status == StepStatus.Passed);
}
