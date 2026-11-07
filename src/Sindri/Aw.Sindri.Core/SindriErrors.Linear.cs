using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-LINEAR-003: the team is not one the profile picked (Linear hub spec 2026-11-04 §5.1, §8). Raised by the lookups read for a team Context holds nothing for while the profile names teams.</summary>
    public static readonly ErrorDefinition LinearTeamNotPicked = new("SND-LINEAR-003", ErrorKind.Forbidden, "Sindri only reads the Linear teams you picked");

    /// <summary>SND-LINEAR-404: the profile names no Linear team and Context holds nothing for the one asked about.</summary>
    public static readonly ErrorDefinition LinearNothingStored = new("SND-LINEAR-404", ErrorKind.NotFound, "Sindri has nothing stored for that Linear team");
}
