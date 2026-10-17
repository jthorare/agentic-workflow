using Aw.Forge.Infrastructure.World;

namespace Aw.Forge.Testing;

/// <summary>The fake forge's world: in memory, gone with the process.</summary>
internal sealed class MemoryWorldStore : IWorldStore
{
    private readonly Lock _gate = new();
    private readonly WorldState _state = new();

    public TResult Read<TResult>(Func<WorldState, TResult> read)
    {
        lock (_gate)
        {
            return read(_state);
        }
    }

    public TResult Write<TResult>(Func<WorldState, TResult> write)
    {
        lock (_gate)
        {
            return write(_state);
        }
    }
}
