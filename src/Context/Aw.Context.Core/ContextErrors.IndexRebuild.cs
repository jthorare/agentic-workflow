using Aw.Kernel;

namespace Aw.Context.Core;

/// <summary>The query's answers while the code index builds, and when it runs out of time (issue 2945: SND-CONTEXT-261..263).</summary>
public static partial class ContextErrors
{
    /// <summary>SND-CONTEXT-261: the symbol field holds something that is not a file path (an identifier, or path#name).</summary>
    public static readonly ErrorDefinition SymbolNotAPath = new("SND-CONTEXT-261", ErrorKind.Invalid, "The symbol field wants a file path");

    /// <summary>SND-CONTEXT-262: the repo's code index is rebuilding, so a miss is not final; the message carries the progress line.</summary>
    public static readonly ErrorDefinition CodeIndexRebuilding = new("SND-CONTEXT-262", ErrorKind.Unavailable, "The code index is rebuilding");

    /// <summary>SND-CONTEXT-263: the query passed 'context.queryBound' before it had an answer.</summary>
    public static readonly ErrorDefinition QueryTimedOut = new("SND-CONTEXT-263", ErrorKind.Timeout, "The context query ran out of time");
}
