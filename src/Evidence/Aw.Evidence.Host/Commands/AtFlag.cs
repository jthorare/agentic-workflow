using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Host.Commands;

/// <summary>Binds a 'base|head' flag value (the side of the change a run is at, recorded in its summary).</summary>
internal static class AtFlag
{
    /// <summary>Absent is fine (null); 'base' or 'head' bind; anything else is a usage error.</summary>
    internal static bool TryParse(string? text, out DataRunAt? at)
    {
        switch (text)
        {
            case null:
                at = null;
                return true;
            case "base":
                at = DataRunAt.Base;
                return true;
            case "head":
                at = DataRunAt.Head;
                return true;
            default:
                at = null;
                return false;
        }
    }
}
