namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>What the instance list is asked for (API §3.8): each member narrows the rows; the counts stay root-wide.</summary>
/// <param name="Root">Keep top-level instances only.</param>
/// <param name="Open">True keeps instances whose state is not terminal, false keeps only the finished ones (newest first); null keeps both.</param>
/// <param name="Recipe">A recipe id, or null for all.</param>
/// <param name="Subject">A subject key or label, or null for all.</param>
/// <param name="Address">One instance's address (to find its opaque id), or null.</param>
internal sealed record InstanceQuery(bool Root = false, bool? Open = null, string? Recipe = null, string? Subject = null, string? Address = null);
