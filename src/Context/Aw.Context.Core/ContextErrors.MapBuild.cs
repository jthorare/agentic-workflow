using Aw.Kernel;

namespace Aw.Context.Core;

/// <summary>The map build slot's code (#2106: SND-CONTEXT-260).</summary>
public static partial class ContextErrors
{
    /// <summary>SND-CONTEXT-260: a map build waited for the build that holds the one build slot and gave up because that build wrote no line for 30 min (#2106); it is not retried.</summary>
    public static readonly ErrorDefinition MapBuildWaitTimedOut = new("SND-CONTEXT-260", ErrorKind.Timeout, "The running map build wrote no line for 30 min");
}
