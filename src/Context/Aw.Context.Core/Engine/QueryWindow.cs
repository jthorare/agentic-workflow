using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>
/// The page of ranked records a caller reads (#3370): the query fetches only the records at ranks <see cref="Offset"/> + 1 to
/// <see cref="Offset"/> + <see cref="Limit"/>, and the answer counts every match. No window = every match is read (#938).
/// </summary>
/// <param name="Offset">How many ranked records to skip (0 or more).</param>
/// <param name="Limit">How many ranked records to read (1 or more).</param>
public sealed record QueryWindow([property: JsonPropertyName("offset")] int Offset, [property: JsonPropertyName("limit")] int Limit);
