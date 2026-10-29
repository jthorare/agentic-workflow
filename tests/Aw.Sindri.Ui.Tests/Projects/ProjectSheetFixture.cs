using System.Net;
using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>What the project sheet tests share: the attached golden as the served card, edited per case, the fake launcher, and the sheet opened on it.</summary>
internal sealed class ProjectSheetFixture
{
    internal const string Attached = "prj_0000000000000000000000000H";
    internal const string Led = "linear:project/00000000-0000-4000-8000-000000000001";

    internal ProjectsFixture Projects { get; } = new();

    internal FakeLauncher Launcher { get; } = new();

    internal FakeSindriApi Server => Projects.Server;

    internal ProjectSheetViewModel Sheet { get; }

    internal ProjectSheetFixture()
    {
        Sheet = new ProjectSheetViewModel(Server, Launcher, Projects.Clock);
    }

    /// <summary>Serves the attached golden after one edit of its card (and of the answer around it) as the card for <paramref name="id"/>.</summary>
    internal void Serve(string id, Action<JsonObject> edit, Action<JsonObject>? answer = null)
    {
        var one = JsonNode.Parse(GoldenFiles.Api("projects-id", "attached"))!.AsObject();   // why: the golden is a JSON object
        var card = one["project"]!.AsObject();
        card["id"] = id;
        edit(card);
        answer?.Invoke(one);
        Server.ProjectAnswers[id] = new RouteAnswer(HttpStatusCode.OK, one.ToJsonString());
    }

    internal static string Subject(string id) => new DeepLink(DeepLinkKind.Project, Uri.EscapeDataString(id)).Subject!;   // why: a project link always has a subject

    /// <summary>Opens the sheet on a card and reads it.</summary>
    internal async Task<ProjectSheetViewModel> Opened(string id, CancellationToken token)
    {
        await Sheet.Open(ProjectsFixture.Url, Subject(id), token);
        return Sheet;
    }
}
