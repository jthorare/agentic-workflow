using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>What a check needs: exactly one of the keys. 'Command' is parsed only to be refused (SND-REVIEW-003).</summary>
/// <param name="Ci">The name of the CI check run that must succeed on the head sha.</param>
/// <param name="App">With 'ci': the GitHub App slug the run must come from.</param>
/// <param name="ChangedWith">Globs; some changed file must match each.</param>
/// <param name="Covers">True: the body has a 'Covers:' line and every ref resolves.</param>
/// <param name="Body">A .NET regex a body line must match.</param>
/// <param name="Manual">True: the owner ticks it on this head.</param>
/// <param name="Command">Refused until the sandbox can run it.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PrReviewNeed(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Ci = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? App = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? ChangedWith = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Covers = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Body = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Manual = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Command = null);
