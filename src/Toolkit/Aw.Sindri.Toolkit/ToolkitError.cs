namespace Aw.Sindri.Toolkit;

/// <summary>One SDK error code: AW-TOOLKIT-NNN and a short title.</summary>
/// <param name="Code">The code.</param>
/// <param name="Title">What the code means.</param>
public sealed record ToolkitError(string Code, string Title);
