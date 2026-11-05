using Aw.Kernel.Testing;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Features.ModelCheck;
using Aw.Sindri.Tests.Workflow.BuildShip;

namespace Aw.Sindri.Tests.Workflow.ModelCheck;

/// <summary>Doctor warns when the profile runs scripts/tlc-check.sh as a build command, since the build already runs the model check.</summary>
[Lane(TestLane.Unit)]
public sealed class ModelCheckDoubleRunDoctorTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_profile_that_runs_tlc_check_as_a_build_command_gets_a_warning_with_the_fix()
    {
        var rows = await new ModelCheckDoubleRunDoctor(new StubCommandSource(["dotnet build", "scripts/tlc-check.sh --timeout 120"], TimeSpan.FromHours(2))).Run(Token);

        var row = Assert.Single(rows);
        Assert.Equal(("model:double-run", CheckState.Warn), (row.Name, row.State));
        Assert.Contains("every spec runs twice", row.Detail, StringComparison.Ordinal);
        Assert.Equal("remove scripts/tlc-check.sh from workflow.build.commands", row.Fix);
    }

    [Fact]
    public async Task A_profile_without_it_gets_no_row()
    {
        var rows = await new ModelCheckDoubleRunDoctor(new StubCommandSource(["dotnet build", "dotnet test"], TimeSpan.FromHours(2))).Run(Token);

        Assert.Empty(rows);
    }
}
