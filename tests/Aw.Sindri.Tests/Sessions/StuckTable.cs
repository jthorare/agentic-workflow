using Aw.Kernel;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>A process table over the fake whose whole-table read, or one pid's environment, can be made to wait until released.</summary>
internal sealed class StuckTable(FakeProcessTable inner) : IProcessTable, IDisposable
{
    private readonly ManualResetEventSlim _gate = new(initialState: false);
    private readonly HashSet<int> _stuck = [];
    public ReadSignal Tables { get; } = new();

    public ReadSignal Environments { get; } = new();

    public int Reads => Tables.Count;

    public bool BlockReads { get; set; }

    public void StickEnvironment(int pid) => _ = _stuck.Add(pid);

    public void Release() => _gate.Set();

    /// <summary>Makes released reads wait again, for a test that blocks the table a second time.</summary>
    public void Rearm() => _gate.Reset();

    public Result<IReadOnlyList<ProcessEntry>> Read(CancellationToken cancellationToken)
    {
        Tables.Increment();
        if (BlockReads)
        {
            _ = _gate.Wait(TimeSpan.FromSeconds(10), CancellationToken.None);
        }

        return inner.Read(cancellationToken);
    }

    public IReadOnlyDictionary<string, string> Environment(int pid)
    {
        if (_stuck.Contains(pid))
        {
            Environments.Increment();
            _ = _gate.Wait(TimeSpan.FromSeconds(10), CancellationToken.None);
        }

        return inner.Environment(pid);
    }

    public string? Cwd(int pid) => inner.Cwd(pid);

    public IReadOnlyList<string> OpenFiles(int pid) => inner.OpenFiles(pid);

    public void Dispose() => _gate.Dispose();
}
