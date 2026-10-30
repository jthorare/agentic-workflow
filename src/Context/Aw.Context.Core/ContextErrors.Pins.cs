using Aw.Kernel;

namespace Aw.Context.Core;

/// <summary>The concept pin write's errors, SND-CONTEXT-171 to 177 (concept pins spec §3.6, "The write").</summary>
public static partial class ContextErrors
{
    /// <summary>SND-CONTEXT-171: a pin write names a name, summary or alias the profile's rules refuse (concept pins spec §3.6). The message starts with the key path ('index.concepts[2].name: …'); nothing was written.</summary>
    public static readonly ErrorDefinition PinInvalid = new("SND-CONTEXT-171", ErrorKind.Invalid, "A pin is not valid");

    /// <summary>SND-CONTEXT-172: a pin names a feature that is not in the current published map. The message names the key and the map version; nothing was written.</summary>
    public static readonly ErrorDefinition PinFeatureUnknown = new("SND-CONTEXT-172", ErrorKind.Invalid, "A pin names a feature that is not in the map");

    /// <summary>SND-CONTEXT-173: two pins of one write would get the same id. The message names both names and the id; nothing was written.</summary>
    public static readonly ErrorDefinition PinIdClash = new("SND-CONTEXT-173", ErrorKind.Invalid, "Two pins would get the same id");

    /// <summary>SND-CONTEXT-174: an unpin names an id no pin in the profile has. The message lists the nearest ids.</summary>
    public static readonly ErrorDefinition PinNotInProfile = new("SND-CONTEXT-174", ErrorKind.Invalid, "No pin has that id");

    /// <summary>SND-CONTEXT-175: an unpin would drop a pin's 'paths', 'dependsOn' or 'owns' without being told to ('--with-folder-rules').</summary>
    public static readonly ErrorDefinition PinHasFolderRules = new("SND-CONTEXT-175", ErrorKind.Invalid, "The pin has folder rules set outside the app");

    /// <summary>SND-CONTEXT-176: an unpin names a pin only a repo's own 'index.concepts' holds; the profile write doesn't touch it.</summary>
    public static readonly ErrorDefinition PinInRepoProfile = new("SND-CONTEXT-176", ErrorKind.Invalid, "The pin is set in a repo's own profile");

    /// <summary>SND-CONTEXT-177: a '--from-file' line or block has no name to pin. The message names the line; nothing is written.</summary>
    public static readonly ErrorDefinition PinLineUnreadable = new("SND-CONTEXT-177", ErrorKind.Invalid, "A line of the pin list can't be read");
}
