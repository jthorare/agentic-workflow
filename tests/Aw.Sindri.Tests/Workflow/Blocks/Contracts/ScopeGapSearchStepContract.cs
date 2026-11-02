using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>An agent Step with no block runtime: it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeGapSearchStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => GapSearchSteps.GapSearch;

    protected override string ExpectedHash => "sha256:1c800204c3b2dc77cf7c1d65e516d3e533c765a51b900279bb3213c5d6f044f5";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
