namespace Aw.Sindri.Core.Toolkits;

/// <summary>One tool of a toolkit, as its manifest lists it.</summary>
/// <param name="Name">The tool name.</param>
/// <param name="Description">What the tool does.</param>
/// <param name="InputSchema">The JSON schema of the request, as JSON text.</param>
/// <param name="OutputSchema">The JSON schema of the response, as JSON text.</param>
/// <param name="ReadOnly">True when the tool only reads, so a test run may call it for real. False until the manifest sets it.</param>
public sealed record ToolkitTool(string Name, string Description, string InputSchema, string OutputSchema, bool ReadOnly = false);
