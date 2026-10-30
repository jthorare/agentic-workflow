using Aw.Context.Core.Concepts;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Ranking;

/// <summary>The pin leg of fusion (concept pins spec §3.3): the fused items that sit inside the anchor's pin, in fused order.</summary>
public static class PinLeg
{
    /// <summary>The leg's name in 'why': 'rrf:pin#2'.</summary>
    public static SourceId Source { get; } = SourceId.From("pin");

    /// <summary>The fused items inside the pin, in their fused order.</summary>
    /// <param name="fused">The plain fusion's output, best first.</param>
    /// <param name="scope">The pin.</param>
    /// <returns>The leg.</returns>
    public static RankedLeg Of(IReadOnlyList<FusedItem> fused, PinScope scope) => throw new NotSupportedException();
}
