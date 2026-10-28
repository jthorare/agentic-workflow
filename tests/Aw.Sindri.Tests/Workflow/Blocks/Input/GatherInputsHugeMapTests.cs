using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflow.Blocks.Input;

/// <summary>
/// The 20,000-anchor gather (#1101) in a class of its own that never runs beside another test: alone it takes about 7 s, but beside the rest of the suite
/// CPU contention stretched it past the integration lane's 60 s cap (LANE-TEST-001), so the cap now sees its real time.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HugeMap.Name)]
public sealed class GatherInputsHugeMapTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aw-gather-inputs-" + RealTime.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task A_huge_map_fills_whole_and_the_Step_does_not_fail()
    {
        var combined = new GatherInputsFillTests.Combined(a => ContextInputFixtures.SmallSet([.. Enumerable.Range(0, 20000).Select(i => "feature:f" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))]));
        await using var provider = GatherInputsFillTests.Compose(_root, combined);
        var runner = provider.GetRequiredService<IStepInputRunner>();

        var filled = await runner.Fill(GatherInputsFillTests.Context("input.context.lenses"), 1, TestContext.Current.CancellationToken);

        Assert.False(filled.IsError, filled.IsError ? filled.Error.Message : string.Empty);
        Assert.DoesNotContain("more lines not shown", filled.Value.Texts.Sections["lenses"], StringComparison.Ordinal);
        Assert.Equal(20000, filled.Value.Texts.Result!.Value.GetProperty("anchors").GetArrayLength());
    }
}
