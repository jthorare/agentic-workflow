using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Projects;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>Ties the page's Connect Linear offer to the sentence the server really serves, since <see cref="ProjectsLinear.OffersConnect"/> matches that text.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectsLinearGoldenTests
{
    /// <summary>
    /// The served "Linear isn't connected" sentence (Golden/Api/projects/empty-no-linear.json) offers Connect Linear. Rewording the Core sentence
    /// (LinearToken.Missing, ProjectCards.cs:198) fails this test until the app's match follows; the real fix is #2625.
    /// </summary>
    [Fact]
    public void The_served_no_linear_sentence_offers_Connect_Linear()
    {
        var list = AwJson.Deserialize<ProjectList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("projects", "empty-no-linear")), SindriApiClient.Json).Value;

        Assert.True(ProjectsLinear.OffersConnect(list.Empty));
    }
}
