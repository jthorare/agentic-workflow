namespace Aw.Sindri.Core.Graph.Validation;

/// <summary>
/// The graph validator (issue 2258, Part 2 "Validation"): one trigger, every node connected, no cycle, references only to
/// nodes upstream, known block types with a fitting config, a readable cron and timezone, known repos. Pure: the names it
/// can't answer for come through <see cref="GraphValidationContext"/>.
/// </summary>
/// <param name="context">What it consults besides the document.</param>
public sealed class GraphValidator(GraphValidationContext context)
{
    /// <summary>Checks <paramref name="spec"/>.</summary>
    /// <param name="spec">The graph.</param>
    /// <param name="mode">Draft or activation.</param>
    /// <returns>The findings.</returns>
    public GraphValidation Validate(GraphSpec spec, GraphMode mode)
    {
        ArgumentNullException.ThrowIfNull(spec);
        _ = (mode, context);
        throw new NotSupportedException();
    }
}
