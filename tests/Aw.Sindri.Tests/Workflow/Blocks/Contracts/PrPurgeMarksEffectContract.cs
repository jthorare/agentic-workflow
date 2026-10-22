using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The terminal marks cleanup: it hashes, kinds, registers once and resolves under its kind.</summary>
[Lane(TestLane.Unit)]
public sealed class PrPurgeMarksEffectContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PrEffects.PurgeMarks;

    protected override string ExpectedHash => "sha256:TODO-pin-after-first-run";   // TODO: a new descriptor; copy the actual hash from the failing run

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
