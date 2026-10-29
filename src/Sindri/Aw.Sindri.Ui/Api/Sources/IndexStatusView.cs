namespace Aw.Sindri.Ui.Api.Sources;

/// <summary>GET /sindri/index/status: one row per context source (Linear, Slack, GitHub, …), then the concepts counts.</summary>
/// <param name="Sources">The sources, each with where its pull stands.</param>
/// <param name="Concepts">The concepts counts; null from a server that predates them.</param>
internal sealed record IndexStatusView(IReadOnlyList<IndexSourceView> Sources, IndexConceptsView? Concepts = null);
