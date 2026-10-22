using Aw.Kernel.Testing;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Workflow.BuildShip;

/// <summary>The commands of the approved profile (line spec LN-WF9, decision A): its lines split into argv, each allowed workflow.steps.longMax.</summary>
[Lane(TestLane.Unit)]
public sealed class ProfileBuildCommandsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<ProfileBuildCommands> Approved(WorkflowSettings? workflow)
    {
        var profiles = new InMemoryProfiles();
        _ = await profiles.Save(new LoadedProfile(new SindriProfile(1, "thor") with { Workflow = workflow }, ProfileHash.From(new string('a', 64)), "/home/profile.yaml"), Token);
        return new ProfileBuildCommands(profiles);
    }

    [Fact]
    public async Task The_lines_become_argv_in_order_and_each_may_run_as_long_as_longMax()
    {
        var source = await Approved(new(Build: new BuildSettings(["dotnet build -warnaserror", "dotnet test --filter \"Lane=unit\""]), Steps: new StepsSettings("90m")));

        var set = await source.Read(Token);

        Assert.Equal(["dotnet build -warnaserror", "dotnet test --filter \"Lane=unit\""], set.Value.Commands.Select(static c => c.Name), StringComparer.Ordinal);
        Assert.Equal(["dotnet", "test", "--filter", "Lane=unit"], set.Value.Commands[1].Run);
        Assert.Equal(TimeSpan.FromMinutes(90), set.Value.Each);
    }

    [Fact]
    public async Task Without_a_longMax_each_command_gets_two_hours()
    {
        var source = await Approved(new(Build: new BuildSettings(["npm test"])));

        var set = await source.Read(Token);

        Assert.Equal(TimeSpan.FromHours(2), set.Value.Each);
    }

    [Fact]
    public async Task A_profile_without_commands_or_with_no_approval_lists_none()
    {
        var noKey = await Approved(workflow: null);
        var notApproved = new ProfileBuildCommands(new InMemoryProfiles());

        Assert.Empty((await noKey.Read(Token)).Value.Commands);
        Assert.Empty((await notApproved.Read(Token)).Value.Commands);
    }

    [Fact]
    public async Task A_line_that_does_not_split_is_an_error_naming_its_index_with_the_fix_and_not_echoing_it()
    {
        var source = await Approved(new(Build: new BuildSettings(["dotnet build", "dotnet test --token=hunter2 --filter \"a"])));

        var set = await source.Read(Token);

        Assert.True(set.IsError);
        Assert.Contains("workflow.build.commands[1]", set.Error.Message, StringComparison.Ordinal);
        Assert.Contains("(fix: ", set.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", set.Error.Message, StringComparison.Ordinal);
    }
}
