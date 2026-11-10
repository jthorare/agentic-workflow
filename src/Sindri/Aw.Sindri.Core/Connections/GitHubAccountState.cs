namespace Aw.Sindri.Core.Connections;

/// <summary>Where the owner's GitHub connection stands, for the Reviews tab.</summary>
public enum GitHubAccountState
{
    /// <summary>A GitHub connection is connected and its token is readable.</summary>
    Connected,

    /// <summary>A GitHub connection exists but needs sign-in again.</summary>
    NeedsSignIn,

    /// <summary>There is no GitHub connection.</summary>
    Missing,
}
