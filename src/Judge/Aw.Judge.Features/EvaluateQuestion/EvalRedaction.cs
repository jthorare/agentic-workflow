using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Ports;

namespace Aw.Judge.Features.EvaluateQuestion;

/// <summary>What 'judge eval' writes goes through here: values and keys are redacted before anything reaches a file. Also carries the package's JSON options.</summary>
/// <param name="redactor">Strips secrets.</param>
/// <param name="options">The package's AwJson options.</param>
internal sealed class EvalRedaction(IInputRedactor redactor, JsonSerializerOptions options)
{
    /// <summary>The package's AwJson options.</summary>
    public JsonSerializerOptions Options { get; } = options;

    /// <summary>The node as redacted JSON text.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The text.</returns>
    public string Redacted(JsonNode node) => InputJson.Write(redactor.Redact(node), Options);

    /// <summary>The text, redacted.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The redacted text, or empty when it can't be read back.</returns>
    public string RedactedText(string text) => redactor.Redact(JsonValue.Create(text)) is JsonValue value && value.TryGetValue<string>(out var redacted) ? redacted : "";
}
