namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The answer to GET /sindri/index/status: one row per record feed of the approved profile, in source order (none without a feed), and where concept synthesis stands.</summary>
/// <param name="Sources">The rows.</param>
public sealed record IndexStatusView(IReadOnlyList<IndexSourceRow> Sources)
{
    /// <summary>Where concept synthesis stands (#1597); null from a server that predates it. A concepts read that failed still answers, with its code.</summary>
    public IndexConceptsView? Concepts { get; init; }

    /// <summary>The context.db store row (doctor's context:store); null from a server that predates it. A failed read still answers, as a "fail" row.</summary>
    public IndexStoreView? Store { get; init; }
}
