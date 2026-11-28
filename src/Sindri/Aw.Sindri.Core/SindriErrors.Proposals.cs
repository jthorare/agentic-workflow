using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-PROP-001: no proposal has that id.</summary>
    public static readonly ErrorDefinition ProposalNotFound = new("SND-PROP-001", ErrorKind.NotFound, "No such proposal (fix: sindri proposals)");

    /// <summary>SND-PROP-002: the proposal is published, merged or rejected already, so it can't be rejected.</summary>
    public static readonly ErrorDefinition ProposalNotRejectable = new("SND-PROP-002", ErrorKind.Conflict, "The proposal is published, merged or rejected already");
}
