namespace Aw.Sindri.Ui.Api.Todos;

/// <summary>The paths of the todo calls (todo spec §4.4). The id is escaped as one segment.</summary>
internal static class TodoRoutes
{
    private const string Root = "/sindri/todos";

    internal static string List(string state) => $"{Root}?state={Uri.EscapeDataString(state)}";

    internal static string Add => Root;

    internal static string One(string id) => $"{Root}/{Uri.EscapeDataString(id)}";

    internal static string Change(string id) => One(id) + "/change";

    internal static string Done(string id) => One(id) + "/done";

    internal static string Reopen(string id) => One(id) + "/reopen";

    internal static string Remove(string id) => One(id) + "/remove";

    internal static string Restore(string id) => One(id) + "/restore";
}
