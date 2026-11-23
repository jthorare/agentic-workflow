using Aw.Kernel;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Tests.RestartProcess;

/// <summary>An open-set read that answers the runs it was given, the same each call.</summary>
/// <param name="runs">The open runs.</param>
internal sealed class FixedOpenRuns(params OpenRun[] runs) : IOpenRuns
{
    /// <summary>How many times the set was read.</summary>
    public int Reads { get; private set; }

    public ValueTask<Result<IReadOnlyList<OpenRun>>> Read(CancellationToken cancellationToken)
    {
        Reads++;
        return ValueTask.FromResult<Result<IReadOnlyList<OpenRun>>>(runs);
    }
}
