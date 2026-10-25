using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// Tool results (context spec §8.5): the answer's JSON as structuredContent and one short text block that summarises
/// it without repeating its text; a refusal is an isError result whose text and structuredContent carry the SND code.
/// </summary>
internal static class McpResults
{
    /// <summary>A successful call.</summary>
    /// <typeparam name="T">The answer's type (in the host's JSON context).</typeparam>
    /// <param name="services">The request's services (for the host's JSON options).</param>
    /// <param name="value">The answer.</param>
    /// <param name="summary">The text block.</param>
    /// <param name="fence">The free-text fields to fence in the answer, or null when it carries none.</param>
    /// <param name="page">The page of each top-level list, or null for the whole answer (a tool call always passes one).</param>
    /// <returns>The tool result.</returns>
    internal static CallToolResult Success<T>(IServiceProvider services, T value, string summary, FencePlan? fence = null, McpWindow? page = null)
    {
        var content = fence is null ? Element(services, value) : McpFencing.Apply(Element(services, value), fence);
        return new CallToolResult
        {
            StructuredContent = page is { } window ? McpPages.Apply(content, window) : content,
            Content = [new TextContentBlock { Text = summary }],
        };
    }

    /// <summary>A refused call.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="error">The error.</param>
    /// <returns>The tool result.</returns>
    internal static CallToolResult Failure(IServiceProvider services, Error error) => new()
    {
        IsError = true,
        StructuredContent = Element(services, new McpToolError(error.Code, error.Message)),
        Content = [new TextContentBlock { Text = $"{error.Code} {error.Message}" }],
    };

    /// <summary>The result of a service call: its answer summarised, or its error.</summary>
    /// <typeparam name="T">The answer's type.</typeparam>
    /// <param name="services">The request's services.</param>
    /// <param name="result">The service's result.</param>
    /// <param name="summary">Summarises a successful answer.</param>
    /// <param name="fence">The free-text fields to fence in the answer, or null when it carries none.</param>
    /// <param name="page">The page of each top-level list, or null for the whole answer (a tool call always passes one).</param>
    /// <returns>The tool result.</returns>
    internal static CallToolResult From<T>(IServiceProvider services, Result<T> result, Func<T, string> summary, FencePlan? fence = null, McpWindow? page = null) =>
        result.IsError ? Failure(services, result.Error) : Success(services, result.Value, summary(result.Value), fence, page);

    private static JsonElement Element<T>(IServiceProvider services, T value)
    {
        using var document = JsonDocument.Parse(AwJson.Serialize(value, services.GetRequiredService<JsonSerializerOptions>()));
        return document.RootElement.Clone();
    }
}
