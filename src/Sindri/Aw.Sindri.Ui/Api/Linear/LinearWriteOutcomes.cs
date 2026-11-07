using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>Reads a write route's answer as an outcome (Linear hub 1b contract §1 to §3): the refusals that are outcomes become records, every other refusal stays the error.</summary>
internal static class LinearWriteOutcomes
{
    internal const string Conflict = "SND-LINEAR-409";
    internal const string Unknown = "SND-LINEAR-011";
    internal const string RateLimited = "SND-LINEAR-012";

    /// <summary>The applied record, or the outcome the error stands for, or the error.</summary>
    /// <param name="answer">The route's answer read as its applied record.</param>
    /// <param name="json">The options a 409's conflict members are read with.</param>
    /// <typeparam name="T">The applied record.</typeparam>
    /// <returns>The outcome, or the error that is none.</returns>
    internal static Result<LinearWriteOutcome> Of<T>(Result<T> answer, JsonSerializerOptions json)
        where T : LinearWriteOutcome
    {
        ArgumentNullException.ThrowIfNull(answer);
        return answer.IsSuccess ? answer.Value : OfError(answer.Error, json);
    }

    private static Result<LinearWriteOutcome> OfError(Error error, JsonSerializerOptions json)
    {
        // why: a 429 whose retryAfter is missing or doesn't parse has no time to show, so it stays the plain error (its own banner text) instead of an outcome
        return error.Code switch
        {
            Unknown => new LinearWriteUnknown(Text(error, "clientId")),
            RateLimited when Text(error, "retryAfter") is { } at && DateTimeOffset.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var retryAfter) => new LinearWriteRateLimited(retryAfter),
            Conflict => ConflictOf(error, json),
            _ => error,
        };
    }

    private static Result<LinearWriteOutcome> ConflictOf(Error error, JsonSerializerOptions json)
    {
        if (error.Extensions is null)
        {
            return error;
        }

        // The members are written straight from their elements (no node is parsed and printed again).
        using var members = new MemoryStream();
        using (var writer = new Utf8JsonWriter(members))
        {
            writer.WriteStartObject();
            foreach (var (name, value) in error.Extensions)
            {
                writer.WritePropertyName(name);
                value.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        var conflict = AwJson.Deserialize<LinearChangeConflict>(members.ToArray(), json);
        return conflict.IsSuccess ? conflict.Value : error;
    }

    private static string? Text(Error error, string name) =>
        error.Extensions?.Where(m => string.Equals(m.Key, name, StringComparison.Ordinal) && m.Value.ValueKind == JsonValueKind.String).Select(m => m.Value.GetString()).FirstOrDefault();
}
