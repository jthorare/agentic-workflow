using System.Net;
using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Projects;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>What the Projects tests share: the API's goldens as the served list, a clock at the goldens' "now", and edits of a golden as JSON.</summary>
internal sealed class ProjectsFixture
{
    internal static readonly Uri Url = new("http://127.0.0.1:7410/");

    internal FakeSindriApi Server { get; } = new();

    internal FakeTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    internal static JsonNode Golden(string scenario) => JsonNode.Parse(GoldenFiles.Api("projects", scenario))!; // why: the golden is a JSON document

    internal void Serve(string scenario) => Server.ProjectsAnswer = RouteAnswer.Api("projects", scenario);

    internal void Serve(JsonNode list) => Server.ProjectsAnswer = new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());

    /// <summary>A page on the Projects (All) view. The goldens serve blockedOnYou, so a page whose view the owner never picked opens on Needs me with no rows; <paramref name="defaultView"/> keeps the page bare for the tests about that default.</summary>
    internal ProjectsPageViewModel Page(bool defaultView = false)
    {
        var page = new ProjectsPageViewModel(Server, Clock);
        if (!defaultView)
        {
            page.SetView(ProjectView.All);
        }

        return page;
    }

    /// <summary>A page that is shown and has read the list once, on the Projects view unless <paramref name="defaultView"/>. Grouped by state unless asked otherwise: the goldens carry health and PR lists, so grouped by health the cards would sit in groups the state tests do not expect.</summary>
    internal async Task<ProjectsPageViewModel> Shown(CancellationToken token, ProjectGrouping grouping = ProjectGrouping.State, bool defaultView = false)
    {
        var page = Page(defaultView);
        page.SetGrouping(grouping);
        page.SetShown(shown: true);
        await page.Refresh(Url, token);
        return page;
    }

    /// <summary>The first live card of a golden after one edit, as the page draws it.</summary>
    internal async Task<ProjectCardViewModel> FirstCard(Action<JsonNode> edit, CancellationToken token)
    {
        var list = Golden("mixed");
        edit(list["projects"]![0]!);
        Serve(list);
        return (await Shown(token)).Groups[0].Cards[0];
    }
}
