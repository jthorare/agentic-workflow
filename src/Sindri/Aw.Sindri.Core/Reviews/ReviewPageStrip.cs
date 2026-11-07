namespace Aw.Sindri.Core.Reviews;

/// <summary>One part of the summary strip.</summary>
/// <param name="Panel">The panel it jumps to.</param>
/// <param name="Text">Its words.</param>
public sealed record ReviewPageStrip(
    string Panel,
    string Text);
