using Aw.Kernel;
using Aw.Sindri.Ui.Api.Resources;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The resource calls of the fake: settable rows and a purge that works or is refused, each call recorded.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<ResourceView> _resources = [];
    private readonly List<string> _resourceCalls = [];

    /// <summary>When set, PurgeResources answers this refusal (the rows stay).</summary>
    public Error? PurgeResourcesError { get; set; }

    /// <summary>When set, ListResources answers this error.</summary>
    public Error? ListResourcesError { get; set; }

    /// <summary>When set, ListResources waits for it before it answers (the read stays pending).</summary>
    public TaskCompletionSource? ListResourcesGate { get; set; }

    /// <summary>When set, PurgeResources records its call and then waits for it (the purge stays in flight).</summary>
    public TaskCompletionSource? PurgeGate { get; set; }

    /// <summary>Every resource call, as 'list' or 'purge owner'.</summary>
    public IReadOnlyList<string> ResourceCallLog { get { lock (_gate) { return [.. _resourceCalls]; } } }

    /// <summary>Makes the resources exist, replacing the ones there.</summary>
    /// <param name="resources">The resources.</param>
    public void SeedResources(params ResourceView[] resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        lock (_gate)
        {
            _resources.Clear();
            _resources.AddRange(resources);
        }
    }

    public async Task<Result<ResourceList>> ListResources(Uri api, string? owner, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _resourceCalls.Add("list");
        }

        if (ListResourcesGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (ListResourcesError is { } failed)
        {
            return failed;
        }

        if (NotReceived<ResourceList>(api) is { } unanswered)
        {
            return unanswered;
        }

        lock (_gate)
        {
            return new ResourceList([.. _resources.Where(r => owner is null || string.Equals(r.Owner, owner, StringComparison.Ordinal))]);
        }
    }

    public async Task<Result<PurgeResourcesResult>> PurgeResources(Uri api, string owner, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _resourceCalls.Add("purge " + owner);
        }

        if (PurgeGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        lock (_gate)
        {
            if (PurgeResourcesError is { } refused)
            {
                return refused;
            }

            var removed = _resources.RemoveAll(r => string.Equals(r.Owner, owner, StringComparison.Ordinal));
            return new PurgeResourcesResult(owner, removed);
        }
    }
}
