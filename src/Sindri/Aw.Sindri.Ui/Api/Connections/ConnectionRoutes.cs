namespace Aw.Sindri.Ui.Api.Connections;

/// <summary>The paths of the connection calls. The name is escaped as one segment (a connection name holds ':').</summary>
internal static class ConnectionRoutes
{
    /// <summary>The list and the add.</summary>
    internal const string Root = "/sindri/connections";

    internal static string One(string name) => $"{Root}/{Uri.EscapeDataString(name)}";

    internal static string Test(string name) => $"{One(name)}/test";

    internal static string SignIn(string name) => $"{One(name)}/signin";
}
