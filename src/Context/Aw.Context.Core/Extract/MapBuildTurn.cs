namespace Aw.Context.Core.Extract;

/// <summary>A hold on the <see cref="MapBuildGate"/>; disposing it releases the gate.</summary>
public sealed class MapBuildTurn : IDisposable
{
    private readonly MapBuildGate _gate;
    private int _released;

    internal MapBuildTurn(MapBuildGate gate, bool waited)
    {
        _gate = gate;
        Waited = waited;
    }

    /// <summary>True when the gate was held by another caller when this one asked, so the build ahead of it may have saved what this one is about to map.</summary>
    public bool Waited { get; }

    /// <summary>Releases the gate (once).</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _released, 1) == 0)
        {
            _gate.Leave();
        }
    }
}
