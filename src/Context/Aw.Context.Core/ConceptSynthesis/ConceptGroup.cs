namespace Aw.Context.Core.ConceptSynthesis;

/// <summary>Records that sit close together in embedding space.</summary>
/// <param name="Members">The members, sorted by record key (ordinal).</param>
/// <param name="MembershipHash">Lowercase hex sha256 over the sorted lines "record\tversion\n".</param>
public sealed record ConceptGroup(IReadOnlyList<RecordVector> Members, string MembershipHash)
{
    /// <summary>How many distinct sources the members come from.</summary>
    public int SourceCount
    {
        get
        {
            _ = Members;
            throw new NotSupportedException();
        }
    }
}
