using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.ModelCheck;

namespace Aw.Sindri.Tests.Workflow.ModelCheck;

/// <summary>Where the model check finds its runner and what it hands tlc.sh: the heap is the owner's TLC_HEAP or 2g, and no runner file means no runner.</summary>
[Lane(TestLane.Unit)]
public sealed class ModelCheckSetupsTests
{
    private static Func<string, string?> Env(params (string Name, string Value)[] values) => name => values.Where(v => v.Name == name).Select(static v => v.Value).FirstOrDefault();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void An_unset_or_empty_TLC_HEAP_is_2g(string? heap)
    {
        var setup = ModelCheckSetups.Read(Env(("AW_STATE_DIR", "/nowhere/state"), ("TLC_HEAP", heap ?? string.Empty)));

        Assert.Equal("2g", setup.TlcHeap);
    }

    [Fact]
    public void The_owners_TLC_HEAP_is_passed_on()
    {
        var setup = ModelCheckSetups.Read(Env(("AW_STATE_DIR", "/nowhere/state"), ("TLC_HEAP", "6g")));

        Assert.Equal("6g", setup.TlcHeap);
    }

    [Fact]
    public void No_runner_file_means_no_runner()
    {
        var setup = ModelCheckSetups.Read(Env(("AW_STATE_DIR", "/nowhere/state"), ("AW_TLC_CHECK", "/nowhere/tlc-check.sh")));

        Assert.Null(setup.Runner);
    }
}
