namespace Aw.Sindri.Core.Setup;

/// <summary>A folder the owner picked, checked and resolved.</summary>
/// <param name="Checkout">The real path of its main checkout (a linked worktree resolves to the repository's).</param>
/// <param name="Git">What git says of it now.</param>
public sealed record InspectedRepo(string Checkout, RepoGit Git);
