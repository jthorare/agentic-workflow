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

    public Task<Result<ResourceList>> ListResources(Uri api, string? owner, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _resourceCalls.Add("list");
        }

        if (NotReceived<ResourceList>(api) is { } unanswered)
        {
            return Task.FromResult(unanswered);
        }

        lock (_gate)
        {
            return Task.FromResult<Result<ResourceList>>(new ResourceList([.. _resources.Where(r => owner is null || string.Equals(r.Owner, owner, StringComparison.Ordinal))]));
        }
    }

    public Task<Result<PurgeResourcesResult>> PurgeResources(Uri api, string owner, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _resourceCalls.Add("purge " + owner);
            if (PurgeResourcesError is { } refused)
            {
                return Task.FromResult<Result<PurgeResourcesResult>>(refused);
            }

            var removed = _resources.RemoveAll(r => string.Equals(r.Owner, owner, StringComparison.Ordinal));
            return Task.FromResult<Result<PurgeResourcesResult>>(new PurgeResourcesResult(owner, removed));
        }
    }
}
