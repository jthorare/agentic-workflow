using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Features.StreamEvents;
using Aw.Sindri.Host.Endpoints;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Server;

/// <summary>The event stream's own loop, enumerated directly.</summary>
public sealed class EventStreamTests : IDisposable
{
    private readonly FakeShutdown _shutdown = new();

    public void Dispose() => _shutdown.Dispose();

    [Fact(Timeout = 5_000), Trait("Lane", "unit")]
    public async Task A_stop_during_a_ledger_read_ends_the_stream_cleanly()
    {
        var read = new StoppedMidRead(_shutdown);
        var items = 0;

        await foreach (var _ in EventStream.Events(read, TimeProvider.System, cursor: 0, (CancellationToken.None, _shutdown.Stopping)).WithCancellation(TestContext.Current.CancellationToken))
        {
            items++;
        }

        Assert.Equal((0, 1), (items, read.Reads));
    }

    /// <summary>A ledger read that a stop interrupts: the stop is requested while it runs, and it observes its token.</summary>
    private sealed class StoppedMidRead(FakeShutdown shutdown) : IService<StreamEventsRequest, StreamEventsResponse>
    {
        public int Reads { get; private set; }

        public ValueTask<Result<StreamEventsResponse>> Handle(StreamEventsRequest request, CancellationToken cancellationToken)
        {
            Reads++;
            shutdown.Request();
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<Result<StreamEventsResponse>>(new StreamEventsResponse([]));
        }
    }
}
