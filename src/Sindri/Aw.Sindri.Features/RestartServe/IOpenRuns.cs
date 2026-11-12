namespace Aw.Sindri.Features.RestartServe;

/// <summary>The open agent runs a restart keeps.</summary>
internal interface IOpenRuns
{
    /// <summary>The collector's live sessions that belong to a launch with an instance.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The runs; none before the collector's first pass.</returns>
    public ValueTask<IReadOnlyList<KeptRun>> Read(CancellationToken cancellationToken);
}
