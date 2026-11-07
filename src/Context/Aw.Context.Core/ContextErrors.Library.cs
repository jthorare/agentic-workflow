using Aw.Kernel;

namespace Aw.Context.Core;

/// <summary>The library's profile code (review page spec 2026-11-04 §9: SND-LIB-001; the design-doc codes are <see cref="Library.DesignDocCodes"/>).</summary>
public static partial class ContextErrors
{
    /// <summary>SND-LIB-001: a 'context.sources.library' key is invalid; the message names the key path and the allowed values.</summary>
    public static readonly ErrorDefinition LibraryInvalid = new("SND-LIB-001", ErrorKind.Invalid, "The profile's context.sources.library section is invalid");
}
