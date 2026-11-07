namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>One part of the summary strip: the panel it jumps to and its words ("3 open", "Covers Ph5, REQ-E3", "No doc named").</summary>
internal sealed record ReviewStripPartView(string Panel, string Text);
