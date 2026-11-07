namespace Aw.Sindri.Core.Reviews;

/// <summary>The list's error line, shown above the last rows.</summary>
/// <param name="Text">"PR list is from 23:45: github failed: SND-INDEX-401".</param>
public sealed record ReviewStale(string Text);
