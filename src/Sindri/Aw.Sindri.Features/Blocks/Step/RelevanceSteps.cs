using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The relevance review's three Steps (issue 966 spec 4.1 to 4.3), desk-capable: an agent labels every entry the map returned, a judge labels the same entries without seeing those labels, and a deterministic Step drops what
/// both call off topic. Fact and outcome names are camelCase.
/// </summary>
internal static class RelevanceSteps
{
    /// <summary>'step.scope.relevance-review': an agent labels every returned entry in, out or noise, with a reason and a citation; 'reviewed'.</summary>
    // TODO(W9/L-17): read-only capability (no descriptor field yet; the launch enforces it)
    public static BlockDescriptor Review { get; } = AgentStep("step.scope.relevance-review", Capability.Tools)
        .Takes(Text("model"))
        .Reads("fact:brief", "fact:lenses", "fact:coverage", "fact:anchors")
        .Reports("reviewed").Writes(On("fact:relevanceVerdicts", "reviewed"));

    /// <summary>'step.scope.relevance-judge': a judge rules on each entry from a projection that holds none of the agent's labels; 'ruled'. A long Step: one call per entry.</summary>
    public static BlockDescriptor Judge { get; } = BlockStep("step.scope.relevance-judge", Capability.None)
        .Takes(
            new ParameterSpec("parallel", ParameterType.Number, Required: false, IsCapabilityField: false),
            new ParameterSpec("threshold", ParameterType.Number, Required: false, IsCapabilityField: true),
            new ParameterSpec("providers", ParameterType.List, Required: false, IsCapabilityField: true))
        .Reads("fact:brief", "fact:lenses", "fact:relevanceVerdicts")
        .Reports("ruled").Long(TimeSpan.FromMinutes(90)).Writes(On("fact:relevanceRulings", "ruled"));

    /// <summary>'step.scope.relevance-apply': an entry leaves the lenses and the coverage only when the agent and the judge agree it is not in and the agent's citation resolves; 'applied'.</summary>
    public static BlockDescriptor Apply { get; } = BlockStep("step.scope.relevance-apply", Capability.None)
        .Reads("fact:lenses", "fact:coverage", "fact:brief", "fact:relevanceVerdicts", "fact:relevanceRulings")
        .Reports("applied")
        .Writes(On("fact:lenses", "applied"), On("fact:coverage", "applied"), On("fact:relevance", "applied"), On("fact:mapFixes", "applied"));
}
