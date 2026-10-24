using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Context;

/// <summary>
/// The <c language="text">noMap</c> member of the whole map's SND-CONTEXT-030 refusal: there is no map. Every field is optional, and an
/// unknown one is omitted, never null (so it is written back omitted).
/// </summary>
/// <param name="Repos">The repositories the map would cover.</param>
/// <param name="ApprovedAt">When the profile was approved.</param>
/// <param name="FirstRunStartedAt">When the first map build started; set means a first build is going.</param>
internal sealed record MapNotBuiltView(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Repos = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? ApprovedAt = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? FirstRunStartedAt = null);
