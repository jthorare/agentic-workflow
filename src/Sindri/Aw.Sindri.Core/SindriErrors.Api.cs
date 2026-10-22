using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-API-010: a command that reads views was refused while the views rebuild; it wasn't run.</summary>
    public static readonly ErrorDefinition ViewsRebuilding = new("SND-API-010", ErrorKind.Unavailable, "Views are rebuilding; the command wasn't run, retry");

    /// <summary>SND-API-011: the views didn't reach the sequence a caller waits for (X-Sindri-After, or a command's head) in time.</summary>
    public static readonly ErrorDefinition ViewsBehind = new("SND-API-011", ErrorKind.Unavailable, "The views didn't reach the sequence in time");

    /// <summary>SND-API-012: every event stream a caller may open is open (16, 4 held for the human; API spec §5.4).</summary>
    public static readonly ErrorDefinition StreamsFull = new("SND-API-012", ErrorKind.RateLimited, "Too many event streams are open; re-query every 5 s");

    /// <summary>SND-API-013: a query parameter outside its set: a stats or list window that isn't 7d, 30d or 90d, or a hash that isn't a recipe hash (API spec §3.8, E-24, E-25).</summary>
    public static readonly ErrorDefinition InvalidQuery = new("SND-API-013", ErrorKind.Invalid, "A query parameter is outside its set");
}
