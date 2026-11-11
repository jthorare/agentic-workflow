using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Projects;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>Ties the page's Connect Linear offer to the code the server really serves (<see cref="ProjectsLinear.OffersConnect"/> reads the code, never the sentence).</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectsLinearGoldenTests
{
    /// <summary>The served codes of the two Linear empty goldens (empty-no-linear, empty-key-missing) offer Connect Linear; the sentence beside them is only displayed (#2625).</summary>
    [Theory]
    [InlineData("empty-no-linear", "linear-missing", false)]
    [InlineData("empty-key-missing", "linear-key-missing", true)]
    public void The_served_Linear_empty_codes_offer_Connect_Linear(string scenario, string code, bool keyMissing)
    {
        var list = AwJson.Deserialize<ProjectList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("projects", scenario)), SindriApiClient.Json).Value;

        Assert.Equal((code, true, keyMissing), (list.EmptyCode, ProjectsLinear.OffersConnect(list), ProjectsLinear.IsKeyMissing(list)));
    }

    /// <summary>A golden with no code (the syncing one) offers nothing, whatever its sentence says.</summary>
    [Fact]
    public void A_served_list_with_no_code_does_not_offer_Connect_Linear()
    {
        var list = AwJson.Deserialize<ProjectList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("projects", "syncing")), SindriApiClient.Json).Value;

        Assert.False(ProjectsLinear.OffersConnect(list));
    }
}
