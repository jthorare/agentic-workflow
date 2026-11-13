namespace Aw.Sindri.Core.Setup;

/// <summary>What a profile repo's path holds (<see cref="IRepoCheckouts"/>).</summary>
public enum RepoCheckout
{
    /// <summary>The top of a git checkout.</summary>
    Checkout,

    /// <summary>A folder with no '.git' of its own.</summary>
    PlainFolder,

    /// <summary>No folder at the path.</summary>
    Missing,
}
