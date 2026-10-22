namespace Aw.Sindri.Core.Setup;

/// <summary>The catalog action ids a step points at (the app's controls).</summary>
public static class SetupActions
{
    /// <summary>Add a repo to the profile.</summary>
    public const string AddRepo = "AddRepo";

    /// <summary>Approve the profile (protected: the human's).</summary>
    public const string ApproveProfile = "ApproveProfile";

    /// <summary>Approve every registered recipe, children first (protected: the human's).</summary>
    public const string ApproveAllRecipes = "ApproveAllRecipes";

    /// <summary>Register Sindri's MCP server for a repo, local scope only.</summary>
    public const string RegisterMcp = "RegisterMcp";

    /// <summary>Set the managed Onyx up.</summary>
    public const string SetupOnyx = "SetupOnyx";

    /// <summary>Hand the Onyx setup job the owner's login while it waits for it.</summary>
    public const string ConnectOnyx = "ConnectOnyx";

    /// <summary>Build the context map again now (the context lane's job).</summary>
    public const string RefreshContextMap = "RefreshContextMap";
}
