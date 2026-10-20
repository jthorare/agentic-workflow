using System.Text.Json.Nodes;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Plan;

/// <summary>
/// The plan station's checks (line spec §6.3): one pure function over the plan packet's files and the design packet's, run by 'plan check' (draft), the
/// evidence step, stop 2's read model and 'step.plan.freeze'. aw-packets validates the plan packet itself (PKT-PLN-* and PKT's task rules, whose answer comes in as
/// 'validation.json'); these rules check what only the design and the profile can say. A rule whose files are absent is not-run.
/// </summary>
internal static partial class PlanChecks
{
    private const string Tasks = "tasks.json";

    /// <summary>Runs every rule of the stage.</summary>
    /// <param name="facts">The files.</param>
    /// <param name="stage">Draft runs the draft rules; evidence runs all.</param>
    internal static LineCheckResponse Run(LineFacts facts, LineStage stage) => LineChecks.Run("plan", PlanRules.All, facts, stage);

    private static IEnumerable<JsonNode> Phases(LineFacts facts) => LineJson.Items(facts.Json(Tasks), "tasks");

    private static string PhaseId(JsonNode phase) => LineJson.Str(phase, "id") ?? string.Empty;

    /// <summary>The phase's own items: its acceptance entries that are not hidden scenarios.</summary>
    private static IEnumerable<JsonNode> OwnItems(JsonNode phase) => LineJson.Items(phase, "acceptance").Where(static a => LineJson.Bool(a, "holdout") != true);

    private static IEnumerable<JsonNode> LiveDesignItems(LineFacts facts) =>
        LineJson.Items(facts.Json("design/acceptance.json"), "items").Where(static i => !string.Equals(LineJson.Str(i, "status"), "superseded", StringComparison.Ordinal));

    /// <summary>The profile's values for this run (workflow.plan.maxPhaseSize, workflow.line.maxStack, workflow.prove.holdoutMin), from 'options.json' when it is there.</summary>
    private static string MaxPhaseSize(LineFacts facts) => LineJson.Str(facts.Json("options.json"), "maxPhaseSize") ?? "M";

    private static int MaxStack(LineFacts facts) => LineJson.Number(facts.Json("options.json"), "maxStack") ?? 4;

    private static int HoldoutMin(LineFacts facts) => LineJson.Number(facts.Json("options.json"), "holdoutMin") ?? 2;
}
