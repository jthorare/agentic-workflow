using Aw.Kernel;

namespace Aw.Sindri.Host.IndexSources;

/// <summary>The one read 'index connect linear' checks a key with: the team keys the key can read.</summary>
internal interface ILinearTeamReader
{
    /// <summary>Reads the team keys the key can see.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The team keys; SND-CONTEXT-071 when Linear refuses the key; SND-CONTEXT-010 when Linear is unreachable, rate limited or answers an error; SND-CONTEXT-072 for a reply of an unexpected shape.</returns>
    public ValueTask<Result<IReadOnlyList<string>>> Teams(CancellationToken cancellationToken);

    /// <summary>Reads the teams the key can see with their ids and names (Settings, Connections, Linear).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The teams in Linear's order; the same errors as <see cref="Teams"/>.</returns>
    public ValueTask<Result<IReadOnlyList<LinearTeamDetail>>> Details(CancellationToken cancellationToken);
}
