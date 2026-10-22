using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PrMergeEffectContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PrEffects.Merge;

    protected override string ExpectedHash => "sha256:TODO-repin-after-first-run"; // TODO: fact:merge Output moved the descriptor hash; copy the actual hash from the failing run

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
