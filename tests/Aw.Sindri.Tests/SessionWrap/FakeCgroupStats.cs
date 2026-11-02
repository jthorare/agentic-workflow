using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Tests.SessionWrap;

internal sealed class FakeCgroupStats : ICgroupStats
{
    private int _reads;

    /// <summary>What the next reads answer; null is a scope that is gone.</summary>
    public CgroupSample? Sample { get; set; }

    public int Reads => Volatile.Read(ref _reads);

    public CgroupSample? Read(string cgroupPath)
    {
        _ = Interlocked.Increment(ref _reads);
        return Sample;
    }
}
