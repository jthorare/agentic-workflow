using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Context;

/// <summary>
/// The <c language="text">building</c> member of the whole map's SND-CONTEXT-030 refusal: a map build is running. Every field is optional,
/// and an unknown one is omitted, never null (so it is written back omitted).
/// </summary>
/// <param name="SourcesDone">The sources read so far.</param>
/// <param name="SourcesTotal">The sources the build reads.</param>
/// <param name="FilesDone">The files of the current repo read so far.</param>
/// <param name="FilesTotal">The files the build reads in the current repo.</param>
/// <param name="StartedAt">When the build started.</param>
/// <param name="LastSyncAt">When the last map was synced.</param>
/// <param name="JobId">The build's job.</param>
internal sealed record MapBuildingView(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? SourcesDone = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? SourcesTotal = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? FilesDone = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? FilesTotal = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? StartedAt = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? LastSyncAt = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? JobId = null);
