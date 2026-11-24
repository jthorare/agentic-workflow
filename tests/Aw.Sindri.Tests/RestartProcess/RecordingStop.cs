using Aw.Kernel;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Tests.RestartProcess;

/// <summary>A stop that records each call and answers pid 4242, epoch 7.</summary>
internal sealed class RecordingStop : IServeStop
{
    /// <summary>How many times the stop ran.</summary>
    public int Calls { get; private set; }

    public ValueTask<Result<ServeStopped>> Stop(CancellationToken cancellationToken)
    {
        Calls++;
        return ValueTask.FromResult<Result<ServeStopped>>(new ServeStopped(4242, 7, Sequence: 12));
    }
}
