using System.Text;
using System.Text.Json;
using Aw.Kernel.Json;

namespace Aw.Transcripts.Infrastructure.Wire;

/// <summary>The AwJson options over <see cref="TranscriptWireJsonContext"/>, and the one way a line is read.</summary>
internal static class WireJson
{
    public static JsonSerializerOptions Options { get; } = AwJson.CreateOptions(TranscriptWireJsonContext.Default);

    /// <summary>A line as <typeparamref name="T"/>, or null when it is not a JSON object of that shape.</summary>
    public static T? Read<T>(string line)
        where T : class =>
        line.AsSpan().TrimStart().StartsWith("{", StringComparison.Ordinal) && AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(line), Options) is { IsSuccess: true } read
            ? read.Value
            : null;
}
