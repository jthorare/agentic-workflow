using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The routes U1 adds (<see cref="FakeSindriApi"/>, continued): each answers its <see cref="RouteAnswer"/>.</summary>
internal sealed partial class FakeSindriApi
{
    private int _catalogCalls;
    private int _servicesCalls;
    private int _rebuildCalls;

    /// <summary>GET /sindri/commands's answer: P1's golden catalog by default.</summary>
    public RouteAnswer Catalog { get; set; } = RouteAnswer.Api("commands", "catalog");

    /// <summary>GET /sindri/services's answer: a spec-shaped healthy view by default (P2 isn't built).</summary>
    public RouteAnswer ServiceView { get; set; } = RouteAnswer.SpecShaped("services", "fixture");

    /// <summary>POST /sindri/views/rebuild's answer: a spec-shaped accepted command by default.</summary>
    public RouteAnswer Rebuild { get; set; } = RouteAnswer.SpecShaped("views-rebuild", "accepted");

    /// <summary>When set, each rebuild waits for it before it answers (a command in flight); null answers at once.</summary>
    public TaskCompletionSource? RebuildGate { get; set; }

    /// <summary>How many catalog reads were made.</summary>
    public int CatalogCalls => Volatile.Read(ref _catalogCalls);

    /// <summary>How many services reads were made.</summary>
    public int ServicesCalls => Volatile.Read(ref _servicesCalls);

    /// <summary>How many rebuild commands were sent.</summary>
    public int RebuildCalls => Volatile.Read(ref _rebuildCalls);

    public Task<Result<CommandList>> Commands(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _catalogCalls);
        return Task.FromResult(Answer<CommandList>(api, "/sindri/commands", Catalog));
    }

    public Task<Result<ServiceList>> Services(Uri api, CancellationToken cancellationToken) => Services(api, after: null);

    /// <summary>A services call that carries <paramref name="after"/> as X-Sindri-After (see <see cref="Status(Uri, long?, CancellationToken)"/>).</summary>
    /// <param name="api">The API.</param>
    /// <param name="after">The sequence the request carried, or null for a direct call.</param>
    /// <returns>The answer.</returns>
    internal Task<Result<ServiceList>> Services(Uri api, long? after)
    {
        _ = Interlocked.Increment(ref _servicesCalls);
        var carried = Carried(after, unreachable: Down);
        return Task.FromResult(!Down && Refusal is null && carried > 0 && Lag != ViewsLag.None ? Behind(carried) : Answer<ServiceList>(api, "/sindri/services", ServiceView));
    }

    public async Task<Result<CommandAcceptedView>> RebuildViews(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _rebuildCalls);
        if (RebuildGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        var accepted = Answer<CommandAcceptedView>(api, "/sindri/views/rebuild", Rebuild);
        if (accepted.IsSuccess)
        {
            _ = Interlocked.Exchange(ref _pending, accepted.Value.Sequence);
        }

        return accepted;
    }

    // What the real client makes of the same answer: the record, or the refusal's code, detail and kind as served (the
    // client's own reading, SindriApiClient.Refusal, so the two cannot drift), or an answer it can't read.
    private Result<T> Answer<T>(Uri api, string path, RouteAnswer answer)
    {
        if (Down)
        {
            return SindriUiErrors.Unreachable.With($"nothing answers at {api.Authority}");
        }

        var body = System.Text.Encoding.UTF8.GetBytes(answer.Body);
        if (Refusal is { } refusal)
        {
            return Refused<T>(refusal);
        }

        if (SindriApiClient.Refusal(answer.Status, body) is { } refused)
        {
            return refused;
        }

        if (!answer.IsSuccess)
        {
            return SindriUiErrors.BadStatus.With(SindriErrorReading.BadStatusMessage(path, answer.Status));
        }

        var read = AwJson.Deserialize<T>(body, SindriApiClient.Json);
        return read.IsSuccess ? read : SindriUiErrors.BadAnswer.With($"{path} answered with something this build can't read");   // as the client's Read does
    }
}
