using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Setup;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>The states Sindri serves beyond the first run (setup slice 5): a map that retries, a profile that waits and the repo lines of a done agents step, each read from the API's own golden.</summary>
[Lane(TestLane.Unit)]
public sealed class SetupServedStatesTests
{
    private readonly SetupFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<SetupStepViewModel> StepOf(string scenario, string id)
    {
        _f.Serve(scenario);
        return (await _f.Read(Token)).Steps.Single(s => s.Id == id);
    }

    [Fact]
    public async Task A_map_that_retries_shows_the_served_note_and_the_repos_it_has_done_with_no_button()
    {
        var map = await StepOf("map-retrying", "map");

        Assert.Equal(("Working", "Retrying in 5 minutes: the code index was busy.", "0 of 1 files", false), (map.ChipWord, map.DetailText, map.ProgressText, map.HasAction));
    }

    [Fact]
    public async Task A_saved_profile_is_done_with_its_row_closed_and_no_button()
    {
        var profile = await StepOf("profile-needs-approval", "profile");

        Assert.Equal(("Done", false, false), (profile.ChipWord, profile.IsOpen, profile.HasAction));
    }


    [Fact]
    public async Task A_done_agents_step_lists_the_repo_lines_it_was_served()
    {
        var mcp = await StepOf("all-done", "mcp");

        var line = Assert.Single(mcp.Items);
        Assert.Equal(("demo", "Claude Code"), (line.Repo, line.DetailText));
    }
}
