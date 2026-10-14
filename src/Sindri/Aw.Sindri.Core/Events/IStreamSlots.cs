using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Core.Events;

/// <summary>The open event streams, counted against <see cref="StreamLimits"/> (API spec §5.4; SND-API-012 when full).</summary>
public interface IStreamSlots
{
    /// <summary>Takes a slot for <paramref name="caller"/> when <see cref="StreamLimits.Admits"/> allows; disposing it frees the slot.</summary>
    /// <param name="caller">Who opens the stream.</param>
    /// <param name="slot">The slot, when taken.</param>
    /// <returns>True when a slot was taken.</returns>
    public bool TryEnter(CallerClass caller, out IDisposable slot);

    /// <summary>The streams <paramref name="caller"/>'s class holds now.</summary>
    /// <param name="caller">The caller class.</param>
    /// <returns>The count.</returns>
    public int Open(CallerClass caller);
}
