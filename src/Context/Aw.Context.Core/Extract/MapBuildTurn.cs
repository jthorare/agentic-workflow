namespace Aw.Context.Core.Extract;

/// <summary>A hold on the <see cref="MapBuildGate"/>; disposing it releases the gate. Every line the holder writes goes through <see cref="Progress"/>, which is how a build waiting behind it can tell the build is alive.</summary>
public sealed class MapBuildTurn : IDisposable
{
    private readonly MapBuildGate _gate;
    private int _released;

    internal MapBuildTurn(MapBuildGate gate, bool waited, IProgress<string>? progress)
    {
        _gate = gate;
        Waited = waited;
        Progress = new Stamping(gate, progress);
    }

    /// <summary>True when the gate was held by another caller when this one asked, so the build ahead of it may have saved what this one is about to map.</summary>
    public bool Waited { get; }

    /// <summary>
    /// Where the holder writes its lines while it holds the gate: forwards to the progress given to <see cref="MapBuildGate.Enter"/> (which may have been null) and records that the holder is alive.
    /// A waiter gives up only when this has been silent for <see cref="MapBuildGate.WaitLimit"/>.
    /// </summary>
    public IProgress<string> Progress { get; }

    /// <summary>Releases the gate (once).</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _released, 1) == 0)
        {
            _gate.Leave();
        }
    }

    private sealed class Stamping(MapBuildGate gate, IProgress<string>? inner) : IProgress<string>
    {
        public void Report(string value)
        {
            gate.Stamp();
            inner?.Report(value);
        }
    }
}
