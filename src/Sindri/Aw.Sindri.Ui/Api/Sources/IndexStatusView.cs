namespace Aw.Sindri.Ui.Api.Sources;

/// <summary>GET /sindri/index/status: one row per context source (Linear, Slack, GitHub, …).</summary>
/// <param name="Sources">The sources, each with where its pull stands.</param>
internal sealed record IndexStatusView(IReadOnlyList<IndexSourceView> Sources);
