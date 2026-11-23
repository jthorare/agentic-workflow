using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-MSG-034: a Slack message ts is not seconds, a dot and six digits ('1700000000.000100'); the request named a message that can't exist (400).</summary>
    public static readonly ErrorDefinition MessagesTsInvalid = new("SND-MSG-034", ErrorKind.Invalid, "A Slack message ts is seconds, a dot and six digits");

    /// <summary>The Messages codes (messages spec §10), for <see cref="All"/>.</summary>
    private static ErrorDefinition[] EveryMessages() => [MessagesTsInvalid];
}
