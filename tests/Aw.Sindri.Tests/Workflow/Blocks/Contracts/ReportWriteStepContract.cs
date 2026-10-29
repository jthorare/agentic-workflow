using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ReportWriteStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => BugfixSteps.ReportWrite;

    protected override string ExpectedHash => "sha256:9b3f67219d4a19674af6367886a02da7e24621e6ed5d96133c9e7fb91a3f9aa0";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
