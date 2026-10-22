using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block whose runtime is registered once: it hashes, kinds, registers once and resolves nothing here; its behavior is held by its own tests.</summary>
[Lane(TestLane.Unit)]
public sealed class PrSnapshotStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PrSteps.Snapshot;

    protected override string ExpectedHash => "sha256:7ae7cae364ab7610b56f3b456ced28f20431577c73ce8d7e1604678f419007c4";

    protected override bool HasRuntime => true;

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
