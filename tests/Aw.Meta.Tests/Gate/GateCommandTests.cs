using Aw.Meta.Core.Config;
using Aw.Meta.Core.Gate;

namespace Aw.Meta.Tests.Gate;

public sealed class GateCommandTests
{
    private const string Heavy = "/state/bin/heavy";

    [Fact, Trait("Lane", "unit")]
    public void A_heavy_step_goes_through_the_wrapper_when_one_is_installed()
    {
        var step = new StepConfig("build", ["dotnet", "build"], Heavy: true);

        Assert.Equal([Heavy, "dotnet", "build"], GateCommand.For(step, Heavy));
        Assert.Equal(["dotnet", "build"], GateCommand.For(step, heavy: null));
        Assert.Equal(["dotnet", "build"], GateCommand.For(step with { Heavy = false }, Heavy));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_rerun_template_gets_the_test_filter()
    {
        var step = new StepConfig("unit", ["make", "unit"], Heavy: false, Rerun: ["make", "one", "T={test}"]);

        Assert.Equal(["make", "one", "T=Spike"], GateCommand.Rerun(step, "Spike", Heavy));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_dotnet_test_step_without_a_template_gets_a_method_filter()
    {
        var step = new StepConfig("unit", ["dotnet", "test", "--solution", "X.slnx"], Heavy: true);

        Assert.Equal([Heavy, "dotnet", "test", "--solution", "X.slnx", "--filter-method", "*Spike*"], GateCommand.Rerun(step, "Spike", Heavy));
    }

    [Fact, Trait("Lane", "unit")]
    public void Any_other_step_without_a_template_cannot_run_one_test() =>
        Assert.Null(GateCommand.Rerun(new StepConfig("sync", ["scripts/sync.sh"]), "Spike", Heavy));

    [Theory, Trait("Lane", "unit")]
    [InlineData("scripts/tests/{test}.test.sh", "sindri-pod", "scripts/tests/sindri-pod.test.sh")]
    [InlineData("scripts/tests/{test}.test.sh", "scripts/tests/sindri-pod.test.sh", "scripts/tests/sindri-pod.test.sh")]
    [InlineData("scripts/tests/{test}.test.sh", "config/lib/tests/merge-hook.test.sh", "config/lib/tests/merge-hook.test.sh")]
    [InlineData("T={test}", "Spike", "T=Spike")]
    public void A_template_around_the_test_takes_a_bare_name_or_the_full_path(string word, string test, string expected)
    {
        var step = new StepConfig("bash", ["make"], Heavy: false, Rerun: ["bash", word]);

        Assert.Equal(["bash", expected], GateCommand.Rerun(step, test, Heavy));
    }
}
