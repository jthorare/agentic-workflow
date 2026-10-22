using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block whose runtime is registered once: it hashes, kinds, registers once and resolves nothing here; its behavior is held by its own tests.</summary>
[Lane(TestLane.Unit)]
public sealed class PrMergeEffectContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PrEffects.Merge;

    protected override string ExpectedHash => "sha256:8764a318fd54b017a7c62228e0db4f20bb3e1d2b0c6c3a4aaa747df53ff391a9";

    protected override bool HasRuntime => true;

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
