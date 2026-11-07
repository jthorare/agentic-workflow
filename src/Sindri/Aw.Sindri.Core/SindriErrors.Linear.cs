using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-LINEAR-003: the team is not one the profile picked (Linear hub spec 2026-11-04 §5.1, §8). Raised by the lookups read for a team Context holds nothing for while the profile names teams.</summary>
    public static readonly ErrorDefinition LinearTeamNotPicked = new("SND-LINEAR-003", ErrorKind.Forbidden, "Sindri only reads the Linear teams you picked");

    /// <summary>SND-LINEAR-404: the profile names no Linear team and Context holds nothing for the one asked about.</summary>
    public static readonly ErrorDefinition LinearNothingStored = new("SND-LINEAR-404", ErrorKind.NotFound, "Sindri has nothing stored for that Linear team");

    /// <summary>SND-LINEAR-002: a Linear write with no usable write key (Linear hub spec 2026-11-04 §5.1, §8). Nothing was sent.</summary>
    public static readonly ErrorDefinition LinearWriteKeyMissing = new("SND-LINEAR-002", ErrorKind.Forbidden, "Sindri has no Linear write key");

    /// <summary>SND-LINEAR-006: an id in a Linear write is not one of the team's stored lookups (Linear hub spec 2026-11-04 §5.1, §8). Checked before anything is sent.</summary>
    public static readonly ErrorDefinition LinearIdNotInTeam = new("SND-LINEAR-006", ErrorKind.Invalid, "That id is not one of the team's");

    /// <summary>SND-LINEAR-011: a Linear write was sent and Linear never confirmed it, and the one re-read did not find it either (Linear hub spec 2026-11-04 §5.1, §8). The answer carries outcome 'unknown' and the client id; a retry sends the same one.</summary>
    public static readonly ErrorDefinition LinearNoAnswer = new("SND-LINEAR-011", ErrorKind.Timeout, "Linear didn't confirm it");
}
