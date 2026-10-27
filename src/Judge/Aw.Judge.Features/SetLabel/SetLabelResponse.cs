using Aw.Judge.Core.Ids;

namespace Aw.Judge.Features.SetLabel;

/// <summary>The SetLabel response.</summary>
/// <param name="Item">The labelled item.</param>
/// <param name="Label">The label as stored.</param>
public sealed record SetLabelResponse(EvalItemId Item, string Label);
