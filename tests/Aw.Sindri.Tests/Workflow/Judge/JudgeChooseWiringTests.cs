using Aw.Kernel.Testing;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflow.Judge;

/// <summary>The host's container gives 'step.judge.choose' both image ports: without them the Step would check no place and send any image it was named.</summary>
[Lane(TestLane.Unit)]
public sealed class JudgeChooseWiringTests
{
    [Fact]
    public void The_real_container_wires_the_image_roots_and_files_into_the_judge_choice()
    {
        using var provider = ShippedComposition.Compose();
        using var scope = provider.CreateScope();

        var step = scope.ServiceProvider.GetRequiredService<JudgeChooseStep>();

        Assert.NotNull(step.Roots);
        Assert.NotNull(step.Files);
    }
}
