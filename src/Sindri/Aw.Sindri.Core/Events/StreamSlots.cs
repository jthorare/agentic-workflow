using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Core.Events;

/// <summary>The in-process <see cref="IStreamSlots"/>: counts the open streams by caller class and admits by <see cref="StreamLimits"/>. One per process.</summary>
internal sealed class StreamSlots : IStreamSlots
{
    private readonly Lock _gate = new();
    private int _sessions;
    private int _others;

    public bool TryEnter(CallerClass caller, out IDisposable slot)
    {
        lock (_gate)
        {
            if (!StreamLimits.Admits(_sessions + _others))
            {
                slot = Released.Instance;
                return false;
            }

            Count(caller, 1);
        }

        slot = new Slot(this, caller);
        return true;
    }

    public int Open(CallerClass caller)
    {
        lock (_gate)
        {
            return caller == CallerClass.Session ? _sessions : _others;
        }
    }

    private void Count(CallerClass caller, int by)
    {
        if (caller == CallerClass.Session)
        {
            _sessions += by;
        }
        else
        {
            _others += by;
        }
    }

    private sealed class Released : IDisposable
    {
        public static Released Instance { get; } = new();

        public void Dispose()
        {
        }
    }

    private sealed class Slot(StreamSlots owner, CallerClass caller) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                lock (owner._gate)
                {
                    owner.Count(caller, -1);
                }
            }
        }
    }
}
