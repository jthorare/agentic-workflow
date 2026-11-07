using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>One required check of a settings entry (spec §7.3).</summary>
/// <param name="Id">The check's id.</param>
/// <param name="Title">What it says in the checks panel.</param>
/// <param name="When">Globs that make it apply; null means always.</param>
/// <param name="Need">The one-key object that decides it (<c language="text">{ ci: "ui-tests" }</c>, <c language="text">{ manual: true }</c>); kept as the API wrote it.</param>
internal sealed record ReviewSettingsCheck(
    string Id,
    string Title,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? When = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonElement? Need = null);
