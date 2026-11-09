namespace Aw.Sindri.Core.Toolkits;

/// <summary>Where the toolkits Sindri knows are kept (the 'toolkit' table).</summary>
public interface IToolkitStore
{
    /// <summary>Reads one toolkit.</summary>
    /// <param name="name">The toolkit name.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The toolkit, or null when none has that name.</returns>
    public ValueTask<Toolkit?> Find(string name, CancellationToken cancellationToken);

    /// <summary>Reads every toolkit, sorted by name.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The toolkits.</returns>
    public ValueTask<IReadOnlyList<Toolkit>> List(CancellationToken cancellationToken);

    /// <summary>Inserts or replaces a toolkit by name.</summary>
    /// <param name="toolkit">The toolkit.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that ends when the row is written.</returns>
    public ValueTask Save(Toolkit toolkit, CancellationToken cancellationToken);
}
