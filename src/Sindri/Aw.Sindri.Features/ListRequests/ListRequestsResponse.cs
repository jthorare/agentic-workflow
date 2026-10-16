using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListRequests;

/// <summary>The request list (API spec §3.1).</summary>
/// <param name="Requests">The requests, newest first.</param>
/// <param name="Empty">The EmptyText sentence for the filters when there is none (or the views are stale), else null.</param>
/// <param name="AsOf">How fresh the answer is.</param>
public sealed record ListRequestsResponse(IReadOnlyList<RequestView> Requests, string? Empty, AsOf AsOf);
