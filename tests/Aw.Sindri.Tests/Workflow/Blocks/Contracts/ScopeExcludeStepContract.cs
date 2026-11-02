using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeExcludeStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ScopingSteps.Exclude;

    protected override string ExpectedHash => "sha256:d3b8e7dc99bf72b2655900e968b63111d461fe7188fd1b16fd4dd7b496cee008";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
