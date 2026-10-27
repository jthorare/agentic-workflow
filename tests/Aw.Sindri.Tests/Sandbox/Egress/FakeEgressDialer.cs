using System.Net;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Egress;

/// <summary>A dialer that hands out the server end of a pipe pair and keeps the test's end, so the test plays the upstream.</summary>
internal sealed class FakeEgressDialer : IEgressDialer
{
    public List<(IPAddress Address, int Port)> Dialed { get; } = [];

    public TaskCompletionSource<Stream> Upstream { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Exception? Failure { get; set; }

    public ValueTask<Stream> Dial(IPAddress address, int port, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        Dialed.Add((address, port));
        var (mine, theirs) = JoinedStream.Pair();
        _ = Upstream.TrySetResult(theirs);
        return ValueTask.FromResult(mine);
    }
}
