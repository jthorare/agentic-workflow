namespace Aw.Sindri.Core.Setup;

/// <summary>Reads the pre-commit hook of every repo the approved profile names, at the path git resolves for it (core.hooksPath respected).</summary>
public interface IRepoHookStates
{
    /// <summary>Reads each repo's hook.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One state per repo; a repo that is not a git checkout reads as <see cref="HookKind.None"/>.</returns>
    public ValueTask<IReadOnlyList<RepoHookState>> Read(CancellationToken cancellationToken);
}
