using System.Text;
using System.Text.Json;
using Aw.Judge.Core.Decisions;
using Aw.Kernel.Json;

namespace Aw.Judge.Infrastructure.Sqlite;

/// <summary>
/// The JSON text columns, written and read as their typed values through the host's AwJson profile:
/// decisions.skipped (a list of <see cref="SkippedProvider"/>) and decision_details.probabilities (option to probability)
/// and decision_details.reasons (a list of strings).
/// </summary>
/// <param name="json">The host's Kernel JSON profile (its context knows both column types).</param>
internal sealed class DecisionJson(JsonSerializerOptions json)
{
    internal string Skipped(IReadOnlyList<SkippedProvider> skipped) => Write(skipped);

    internal IReadOnlyList<SkippedProvider> Skipped(string text) => Read<IReadOnlyList<SkippedProvider>>(text, "skipped");

    internal string? Probabilities(IReadOnlyDictionary<string, double>? probabilities) => probabilities is null ? null : Write(probabilities);

    internal IReadOnlyDictionary<string, double>? Probabilities(string? text) =>
        text is null ? null : Read<IReadOnlyDictionary<string, double>>(text, "probabilities");

    internal string? Reasons(IReadOnlyList<string>? reasons) => reasons is null ? null : Write(reasons);

    internal IReadOnlyList<string>? Reasons(string? text) => text is null ? null : Read<IReadOnlyList<string>>(text, "reasons");

    private string Write<T>(T value) => Encoding.UTF8.GetString(AwJson.Serialize(value, json));

    // The judge wrote every stored value itself: one it can't read back is a corrupt database, not an expected error.
    private T Read<T>(string text, string column)
    {
        var read = AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(text), json);
        return read.IsSuccess ? read.Value : throw new InvalidDataException($"Stored {column} column: {read.Error.Message}");
    }
}
