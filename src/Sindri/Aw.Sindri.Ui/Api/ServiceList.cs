namespace Aw.Sindri.Ui.Api;

/// <summary>
/// GET /sindri/services (API spec §3.1, P2): the scheduled services with their state, the view's EmptyText sentence for
/// its state and its asOf, which drive the stalled and rebuilding banners (UI spec §4.1) and the System page's Services tab.
/// </summary>
/// <param name="Empty">The EmptyText sentence for the view's state (empty, rebuilding or stalled), or null when it has rows and is fresh.</param>
/// <param name="AsOf">How fresh the view is.</param>
/// <param name="Services">One row per scheduled service; null when the answer carries none.</param>
internal sealed record ServiceList(string? Empty, AsOfView AsOf, IReadOnlyList<ServiceRowView>? Services = null);
