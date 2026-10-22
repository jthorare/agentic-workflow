using System.Collections.Concurrent;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.ConnectOnyx;

/// <summary>The single-use slots, in memory only (see <see cref="IOnyxLoginSlots"/>).</summary>
internal sealed class OnyxLoginSlots : IOnyxLoginSlots
{
    private readonly ConcurrentDictionary<string, OnyxLogin> _slots = new(StringComparer.Ordinal);

    public void Put(JobId job, OnyxLogin login)
    {
        ArgumentNullException.ThrowIfNull(login);
        _slots[job.Value] = login;
    }

    public OnyxLogin? Take(JobId job) => _slots.TryRemove(job.Value, out var login) ? login : null;

    public void Clear(JobId job) => _ = _slots.TryRemove(job.Value, out _);
}
