using Aw.Kernel;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Infrastructure.Messages;

/// <summary>
/// The row store links resolve against until Home's row store lands (messages spec §9.3, §14 M1g "stub row store"; Home spec §3.1 home_rows): it holds no
/// rows, so every link answers 'none' and stays plain text, which is the spec's answer for a row the store doesn't have.
/// </summary>
internal sealed class NoLinkRows : ILinkRows
{
    public ValueTask<Result<IReadOnlyDictionary<string, LinkRow>>> Find(IReadOnlyCollection<string> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        Result<IReadOnlyDictionary<string, LinkRow>> none = new Dictionary<string, LinkRow>(StringComparer.Ordinal);
        return ValueTask.FromResult(none);
    }
}
