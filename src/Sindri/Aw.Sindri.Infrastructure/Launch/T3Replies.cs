using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Reads a T3 tool result: the structured content on success, the 'OrchestratorMcpFailure' text on an error.</summary>
internal static class T3Replies
{
    /// <summary>The reply in <paramref name="result"/>.</summary>
    /// <typeparam name="T">The structured result's shape.</typeparam>
    /// <param name="result">The tool result.</param>
    /// <param name="json">The T3 context's options.</param>
    /// <returns>The reply, or SND-LAUNCH-025 when neither shape reads (a T3 version Sindri doesn't know).</returns>
    public static Result<T3Reply<T>> Read<T>(CallToolResult result, JsonSerializerOptions json)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsError == true)
        {
            var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? string.Empty;
            var failure = AwJson.Deserialize<T3Wire.Failure>(Encoding.UTF8.GetBytes(text), json);
            return failure.IsSuccess
                ? new T3Reply<T>(Value: default, failure.Value)
                : SindriErrors.LaunchT3Unavailable.With($"T3 refused the call: {Shorten(text)}");
        }

        if (result.StructuredContent is not { } content)
        {
            return SindriErrors.LaunchT3Unavailable.With("T3 answered without a structured result (a T3 version Sindri doesn't know)");
        }

        var value = AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(content.GetRawText()), json);
        return value.IsSuccess
            ? new T3Reply<T>(value.Value, Failure: null)
            : SindriErrors.LaunchT3Unavailable.With($"T3's result didn't read: {value.Error.Message}");
    }

    private static string Shorten(string text) => text.Length <= 200 ? text : text[..200] + "…";
}
