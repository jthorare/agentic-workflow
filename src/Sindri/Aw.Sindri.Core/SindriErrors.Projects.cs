using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-PROJECT-001: the project is already on the owner's page, led or attached (projects page spec §8).</summary>
    public static readonly ErrorDefinition ProjectAlreadyOnPage = new("SND-PROJECT-001", ErrorKind.Conflict, "That project is already on your page.");

    /// <summary>SND-PROJECT-002: Context can't answer for the Linear projects (page state 3: attached and hand-made cards by name only).</summary>
    public static readonly ErrorDefinition ProjectFactsUnavailable = new("SND-PROJECT-002", ErrorKind.Unavailable, "Linear and GitHub data unavailable");

    /// <summary>SND-PROJECT-003: a named Linear project is not in the store; the message lists the synced teams.</summary>
    public static readonly ErrorDefinition ProjectNotSynced = new("SND-PROJECT-003", ErrorKind.Invalid, "Sindri doesn't sync that project");

    /// <summary>SND-PROJECT-004: a project field is invalid; the message names the field.</summary>
    public static readonly ErrorDefinition ProjectInvalid = new("SND-PROJECT-004", ErrorKind.Invalid, "A project field is invalid");

    /// <summary>SND-PROJECT-404: no such project.</summary>
    public static readonly ErrorDefinition ProjectNotFound = new("SND-PROJECT-404", ErrorKind.NotFound, "No such project");

    /// <summary>SND-PROJECT-409: the project changed since the caller read it.</summary>
    public static readonly ErrorDefinition ProjectConflict = new("SND-PROJECT-409", ErrorKind.Conflict, "This project changed since you read it. Read it again and retry.");
}
