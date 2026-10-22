using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block whose runtime is registered once: it hashes, kinds, registers once and resolves nothing here; its behavior is held by its own tests.</summary>
[Lane(TestLane.Unit)]
public sealed class PrReplyEffectContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PrEffects.Reply;

    protected override string ExpectedHash => "sha256:ff2ea187d268bff8e2c32affa1061c3e2b79f9dc9b4dd34e494dcdb97ab47dbe";

    protected override bool HasRuntime => true;

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
