namespace Aw.Sindri.Ui.Api.Jobs;

/// <summary>How many of a job's model calls one harness and model answered.</summary>
/// <param name="Harness">The harness ('claude-code', 'codex', 'cursor').</param>
/// <param name="Model">The model.</param>
/// <param name="Count">The calls it answered.</param>
internal sealed record JobModelAnsweredView(string Harness, string Model, int Count);
