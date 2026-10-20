using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>One phase of the plan.</summary>
/// <param name="Phase">The phase, for example P1.</param>
/// <param name="Title">Its title.</param>
/// <param name="Size">XS, S or M.</param>
/// <param name="Kind">feature, fix, refactor, docs or config.</param>
/// <param name="Stack">Its stack.</param>
/// <param name="Wave">Its wave.</param>
/// <param name="Base">Its base rule and ref, for example "dependency-head P1".</param>
/// <param name="WaitsForMerge">Whether it waits for a merge.</param>
/// <param name="Items">The acceptance items it carries.</param>
/// <param name="AllowedFiles">The files it may write.</param>
/// <param name="TestChanges">The existing test files it may change.</param>
/// <param name="HiddenScenarios">How many hidden scenarios guard it (the count only).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StopPhase(
    string Phase,
    string Title,
    string Size,
    string Kind,
    int Stack,
    int Wave,
    string Base,
    bool WaitsForMerge,
    IReadOnlyList<string> Items,
    IReadOnlyList<string> AllowedFiles,
    IReadOnlyList<StopTestChange> TestChanges,
    int HiddenScenarios);
