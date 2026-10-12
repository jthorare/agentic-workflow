using System.Text.Json.Nodes;

namespace Aw.Judge.Core.Ports;

/// <summary>Removes secrets from a question input before it is stored (decision_details.input_json).</summary>
public interface IInputRedactor
{
    /// <summary>
    /// Returns a redacted copy: every string value and every object key has its secrets replaced; numbers, booleans,
    /// nulls and the shape are kept. Redacting the parsed values, not the serialization, keeps the word boundaries an
    /// escaped newline would hide. Never throws on a secret it can't scan: it withholds that text instead.
    /// </summary>
    /// <param name="input">The input (JSON null as null).</param>
    /// <returns>The redacted copy; the input is not changed.</returns>
    public JsonNode? Redact(JsonNode? input);
}
