namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The part of GET /sindri/context/query's answer the Context box reads: the anchors a question resolved to, and the ones it suggests.</summary>
/// <param name="Anchors">Anchors, as served.</param>
/// <param name="SuggestedAnchors">SuggestedAnchors, as served.</param>
internal sealed record QueryAnchorsView(
    IReadOnlyList<string> Anchors,
    IReadOnlyList<string> SuggestedAnchors)
{
    /// <summary>The suggestions with their labels and kinds (#2809); null from a server that does not send them.</summary>
    public IReadOnlyList<QueryChoiceView>? Suggestions { get; init; }
}
