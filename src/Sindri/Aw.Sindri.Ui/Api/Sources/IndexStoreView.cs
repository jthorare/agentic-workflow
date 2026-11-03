namespace Aw.Sindri.Ui.Api.Sources;

/// <summary>The context.db store check of GET /sindri/index/status (#1963): the same row `sindri doctor` shows as context:store.</summary>
/// <param name="State">"ok", "warn" or "fail" (context.db can't be read); anything but "ok" needs a look.</param>
/// <param name="Detail">What the check found, e.g. the log's size against the database's.</param>
/// <param name="Fix">What to do about a warning, or null.</param>
internal sealed record IndexStoreView(string State, string Detail, string? Fix = null);
