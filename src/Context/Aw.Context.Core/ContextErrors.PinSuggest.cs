using Aw.Kernel;

namespace Aw.Context.Core;

/// <summary>The pin-suggest read's code (concept pins spec §3.6, §5: SND-CONTEXT-179).</summary>
public static partial class ContextErrors
{
    /// <summary>SND-CONTEXT-179: a pin-suggest read asks for no mode or several, an empty search, more than 100 lines, a line over 80 characters, or a definition count that is not the line count. The message says which.</summary>
    public static readonly ErrorDefinition PinSuggestInvalid = new("SND-CONTEXT-179", ErrorKind.Invalid, "The pin suggestion is not asked for correctly");
}
