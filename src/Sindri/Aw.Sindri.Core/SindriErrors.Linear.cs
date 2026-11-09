using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-LINEAR-003: the team is not one the profile picked (Linear hub spec 2026-11-04 §5.1, §8). Raised by the lookups read for a team Context holds nothing for while the profile names teams.</summary>
    public static readonly ErrorDefinition LinearTeamNotPicked = new("SND-LINEAR-003", ErrorKind.Forbidden, "Sindri only reads the Linear teams you picked");

    /// <summary>SND-LINEAR-404: the profile names no Linear team and Context holds nothing for the one asked about.</summary>
    public static readonly ErrorDefinition LinearNothingStored = new("SND-LINEAR-404", ErrorKind.NotFound, "Sindri has nothing stored for that Linear team");

    /// <summary>SND-LINEAR-002: a write with no usable write key (Linear hub spec 2026-11-04 §5.1, §8). Nothing was sent. Context raises its own 002 as a 400; the write routes answer 403 so the sheet shows "Edits need write access".</summary>
    public static readonly ErrorDefinition LinearNoWriteKey = new("SND-LINEAR-002", ErrorKind.Forbidden, "Sindri has no Linear write key");

    /// <summary>SND-LINEAR-006: a write named an id that is not in the issue's team's stored lookups (a state, member, label, cycle or parent the team doesn't have). Nothing was sent.</summary>
    public static readonly ErrorDefinition LinearBadValue = new("SND-LINEAR-006", ErrorKind.Invalid, "Not one of the team's Linear values");

    /// <summary>SND-LINEAR-011: a Linear write was sent and Linear never confirmed it, and the one re-read did not find it either (Linear hub spec 2026-11-04 §5.1, §8). The answer carries outcome 'unknown' and the client id; a retry sends the same one.</summary>
    public static readonly ErrorDefinition LinearNoAnswer = new("SND-LINEAR-011", ErrorKind.Timeout, "Linear didn't confirm it");

    /// <summary>SND-LINEAR-001: the write key is the same key as the read key (Linear hub spec 2026-11-04 §8). A key with write access is a second one; the read key stays read-only.</summary>
    public static readonly ErrorDefinition LinearWriteKeySameAsRead = new("SND-LINEAR-001", ErrorKind.Invalid, "The write key is the same as the read key");

    /// <summary>SND-LINEAR-014: no OS keychain took the write key (Linear hub spec 2026-11-04 §8). The write key never goes in a file an agent session can read, so it is not saved.</summary>
    public static readonly ErrorDefinition LinearNoKeychain = new("SND-LINEAR-014", ErrorKind.Invalid, "No keychain to keep the write key in");
}
