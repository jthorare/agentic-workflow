namespace Aw.Sindri.Ui.Api;

/// <summary>A note line that can't sync (todo spec §3.4).</summary>
/// <param name="Line">The 1-based line number.</param>
/// <param name="Reason">Why ("it has a markdown link").</param>
internal sealed record TodoCantSyncView(int Line, string Reason);
