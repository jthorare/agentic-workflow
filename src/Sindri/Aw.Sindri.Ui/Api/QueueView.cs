namespace Aw.Sindri.Ui.Api;

/// <summary>GET /sindri/queue (API spec §3.1 GetQueue, P3): every group in its fixed order, empty ones included (E-18).</summary>
/// <param name="Groups">The groups in the API's order.</param>
/// <param name="Empty">The view's EmptyText sentence for its state (empty, rebuilding or stalled), or null.</param>
/// <param name="AsOf">How fresh the view is.</param>
/// <param name="Tag">The view's tag (also the ETag header): the same tag means the same view, and a read that sends it in If-None-Match gets 304 (#2859).</param>
internal sealed record QueueView(IReadOnlyList<QueueGroupView> Groups, string? Empty, AsOfView AsOf, string? Tag = null);
