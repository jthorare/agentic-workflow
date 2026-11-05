using Aw.Kernel;

namespace Aw.Context.Core;

/// <summary>The map build slot's code (#2106: SND-CONTEXT-260).</summary>
public static partial class ContextErrors
{
    /// <summary>SND-CONTEXT-260: a map build waited 30 min for the build that holds the one build slot and gave up (#2106); it is not retried.</summary>
    public static readonly ErrorDefinition MapBuildWaitTimedOut = new("SND-CONTEXT-260", ErrorKind.Timeout, "Waited 30 min for the running map build");
}
