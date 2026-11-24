using Aw.Kernel;

namespace Aw.Sindri.Core.Messages;

/// <summary>
/// The local rows a link resolves to (messages spec §9.3, #3021): reads the store only, never GitHub, Linear or Slack, so a row the owner's connection could
/// not read is never there. Home's row store (Home spec §3.1, home_rows) is the real source; until it lands the registered store holds no rows, so every
/// link answers 'none'.
/// </summary>
public interface ILinkRows
{
    /// <summary>The rows the store holds for <paramref name="keys"/>; a key with no row is left out.</summary>
    /// <param name="keys">The row keys, each once.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The rows found, by key.</returns>
    public ValueTask<Result<IReadOnlyDictionary<string, LinkRow>>> Find(IReadOnlyCollection<string> keys, CancellationToken cancellationToken);
}
