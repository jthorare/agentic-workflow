namespace Aw.Sindri.Ui.Api.Projects;

/// <summary>The paths of the project calls (projects page spec §6.2). The id is escaped as one segment, so the slashes of a "linear:project/…" key never become segments.</summary>
internal static class ProjectRoutes
{
    private const string Root = "/sindri/projects";

    internal static string List(bool all) => all ? $"{Root}?all=true" : Root;

    internal static string One(string id) => $"{Root}/{Uri.EscapeDataString(id)}";

    internal static string Attach => Root;

    internal static string Change(string id) => One(id) + "/change";

    internal static string Detach(string id) => One(id) + "/detach";

    internal static string Hide => Root + "/hide";

    internal static string Linear => Root + "/linear";
}
