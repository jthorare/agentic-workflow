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

    protected override string ExpectedHash => "sha256:ad2ca916f8f812f0081b047a3d7e7a8f1dc15b3ef7d8e62f0fb52e63ae73c688";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
