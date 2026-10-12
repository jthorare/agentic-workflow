using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>A fact a block writes (spec §2).</summary>
/// <param name="Fact">The fact.</param>
/// <param name="RequiredOn">The outcomes it must be written on; empty means it is optional on every outcome.</param>
public sealed record OutputSpec(FactKey Fact, IReadOnlyList<OutcomeName> RequiredOn);
