using System.Text.Json.Serialization;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Merge;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Owner;

namespace Aw.Meta.Features.Status;

/// <summary>Where the meta stands.</summary>
/// <param name="Obligations">What is owed now.</param>
/// <param name="MergeQueue">Pull requests lanes said are ready, not yet merged.</param>
/// <param name="OwnerQueue">Questions waiting for the owner.</param>
/// <param name="Lanes">The current lane map.</param>
public sealed record StatusResponse(IReadOnlyList<Obligation> Obligations, IReadOnlyList<QueuedPr> MergeQueue, IReadOnlyList<OwnerQuestion> OwnerQueue, LaneMapState Lanes)
{
    /// <summary>The last health check's one-line summary; null when health never ran.</summary>
    public string? LastHealth { get; init; }

    /// <summary>The heavy-slot reservation recomputed for the merge queue now; null when the host could not be read or the team turned the reservation off.</summary>
    public HeavySlots? Heavy { get; init; }

    /// <summary>The running (or last) merge train's summary lines; null when no train ran.</summary>
    public string? Train { get; init; }

    /// <summary>Each lane's context size; null when it could not be read.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<LaneContext>? Context { get; init; }

    /// <summary>The builders per lane to run now; null when the host could not be read.</summary>
    public BuilderAdvice? Builders { get; init; }

    /// <summary>Whether anything is consuming the meta's mail (a beating watch); null when not computed.</summary>
    public string? Consumer { get; init; }
}
