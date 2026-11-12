namespace Aw.Sindri.Core.Setup;

/// <summary>The catalog action ids a step points at (the app's controls).</summary>
public static class SetupActions
{
    /// <summary>Add a repo to the profile.</summary>
    public const string AddRepo = "AddRepo";

    /// <summary>Register Sindri's MCP server for a repo, local scope only.</summary>
    public const string RegisterMcp = "RegisterMcp";

    /// <summary>Connect an index source's token (`sindri index connect &lt;source&gt;`; the human's, at a terminal).</summary>
    public const string ConnectIndexSource = "ConnectIndexSource";

    /// <summary>Build the context map again now (the context lane's job).</summary>
    public const string RefreshContextMap = "RefreshContextMap";

    /// <summary>Restart Sindri onto the installed build (the owner's; first-run spec decision 11).</summary>
    public const string RestartProcess = "RestartProcess";

    /// <summary>Connect one account: a browser sign-in, a device code or a pasted token (the owner's; first-run spec decisions 13 to 17).</summary>
    public const string ConnectAccount = "ConnectAccount";

    /// <summary>
    /// The exact command an agent runs for a step's action, as the catalog row's form says it; null for an owner-only action or no action. A test holds each of these equal to its catalog row,
    /// so the two can't drift.
    /// </summary>
    /// <param name="action">The step's action id, or null.</param>
    /// <returns>The command, or null.</returns>
    public static string? CliFor(string? action) => action switch
    {
        AddRepo => "sindri repo add <path>",
        RegisterMcp => "sindri mcp register --apply",
        ConnectIndexSource => "sindri index connect <source>",
        RefreshContextMap => "sindri context map refresh",
        _ => null,
    };
}
