using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The scoping stage's gap search (issue 966, spec 4.4 to 4.6): descriptors only. The searcher is an agent Step that reads the brief, the acceptance list and the answers and
/// nothing the gather found; its candidates go to a judge, one call each, and the log turns the rulings into the round record and the anchors to look at again.
/// Fact and outcome names are camelCase.
/// </summary>
internal static class GapSearchSteps
{
    private static readonly TimeSpan _judgeLong = TimeSpan.FromMinutes(90);

    /// <summary>'step.scope.gap-search': a different model's own search for what the scope misses, with no view of the lenses, the coverage or the map; 'searched' with its candidates.</summary>
    // TODO(W9/L-17): read-only capability (no descriptor field yet; the launch enforces it)
    public static BlockDescriptor GapSearch { get; } = AgentStep("step.scope.gap-search", Capability.Tools)
        .Takes(Text("model"))
        .Reads("fact:brief", "fact:acceptance", "fact:answerLog")
        .Reports("searched").Writes(On("fact:gapCandidates", "searched"));

    /// <summary>'step.scope.gap-judge': a judge rules on each candidate (real gap, already covered, not in scope) without seeing the searcher's argument; 'ruled'.</summary>
    public static BlockDescriptor GapJudge { get; } = BlockStep("step.scope.gap-judge", Capability.None)
        .Takes(
            new ParameterSpec("parallel", ParameterType.Number, Required: false, IsCapabilityField: false),
            new ParameterSpec("threshold", ParameterType.Number, Required: false, IsCapabilityField: true),
            new ParameterSpec("providers", ParameterType.List, Required: false, IsCapabilityField: true))
        .Reads("fact:brief", "fact:acceptance", "fact:scopeMap", "fact:lenses", "fact:gapCandidates")
        .Reports("ruled")
        .Long(_judgeLong)
        .Writes(On("fact:gapRulings", "ruled"));

    /// <summary>'step.scope.gap-log': appends the round to the gap record; 'clear' when no candidate is a real gap (and the search is closed), 'confirmed' with the findings and the anchors to look at again.</summary>
    public static BlockDescriptor GapLog { get; } = BlockStep("step.scope.gap-log", Capability.None)
        .Reads("fact:gapSearch", "fact:gapCandidates", "fact:gapRulings", "fact:findings")
        .Reports("clear", "confirmed")
        .Writes(On("fact:gapSearch", "clear", "confirmed"), On("fact:gapAnchors", "clear", "confirmed"), On("fact:findings", "confirmed"), On("fact:gapSearchClosed", "clear"));
}
