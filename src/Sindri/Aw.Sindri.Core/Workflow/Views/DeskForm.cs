using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The form of an open desk task (E-20).</summary>
/// <param name="Run">The open run's token, as complete takes it.</param>
/// <param name="Outputs">The outputs asked for.</param>
/// <param name="Checks">The checks run on complete.</param>
/// <param name="CompleteAction">The action id that completes it.</param>
/// <param name="Recommended">The outcome the step before the stop recommends, or null (left out of the JSON) when it made no valid recommendation.</param>
/// <param name="Reviews">What the desk's Step has the person review, from its block's descriptor; null (left out of the JSON) when the block declares none.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeskForm(
    RunToken Run,
    IReadOnlyList<DeskOutput> Outputs,
    IReadOnlyList<string> Checks,
    ActionId CompleteAction,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DeskRecommendation? Recommended = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<DeskReview>? Reviews = null);
