using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Fakes;

/// <summary>One recorded search document.</summary>
/// <param name="Id">Its document id (the issue's UUID for a Linear issue).</param>
/// <param name="Title">Its semantic identifier.</param>
/// <param name="SourceType">Its connector, e.g. 'linear' or 'slack'.</param>
/// <param name="UpdatedAt">When the source last changed it.</param>
/// <param name="SearchDoc">The recorded SearchDoc.</param>
/// <param name="Chunks">Its indexed sections' text, in order.</param>
internal sealed record RecordedDocument(
    string Id, string Title, string SourceType, DateTimeOffset UpdatedAt, JsonObject SearchDoc, IReadOnlyList<string> Chunks);
