using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Fakes;

/// <summary>One recorded Onyx document.</summary>
/// <param name="Id">Its Onyx document id (a Linear issue's UUID for the Linear connector).</param>
/// <param name="Title">Its semantic identifier.</param>
/// <param name="SourceType">Its connector, e.g. 'linear' or 'slack'.</param>
/// <param name="UpdatedAt">When the source last changed it.</param>
/// <param name="SearchDoc">The recorded SearchDoc as Onyx returns it.</param>
/// <param name="Chunks">Its indexed sections' text, in order.</param>
internal sealed record OnyxDocument(
    string Id, string Title, string SourceType, DateTimeOffset UpdatedAt, JsonObject SearchDoc, IReadOnlyList<string> Chunks);
