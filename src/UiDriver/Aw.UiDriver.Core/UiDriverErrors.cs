using Aw.Kernel;

namespace Aw.UiDriver.Core;

#pragma warning disable MA0182 // used by the Avalonia adapter and the CLI, which the analyzer can't see from here

/// <summary>The UI driver's error catalogue (codes AW-UIDRIVER-NNN).</summary>
internal static class UiDriverErrors
{
    /// <summary>AW-UIDRIVER-001: a request is malformed or names something the verb can't take.</summary>
    internal static readonly ErrorDefinition Invalid = new("AW-UIDRIVER-001", ErrorKind.Invalid, "UI driver request is invalid");

    /// <summary>AW-UIDRIVER-002: no node matches the selector.</summary>
    internal static readonly ErrorDefinition NoMatch = new("AW-UIDRIVER-002", ErrorKind.NotFound, "No element matches");

    /// <summary>AW-UIDRIVER-003: the element exists but the verb can't act on it (not a button, not a text box, disabled).</summary>
    internal static readonly ErrorDefinition NotActionable = new("AW-UIDRIVER-003", ErrorKind.Invalid, "Element can't take this action");

    /// <summary>AW-UIDRIVER-004: a wait ran out of time.</summary>
    internal static readonly ErrorDefinition TimedOut = new("AW-UIDRIVER-004", ErrorKind.Timeout, "Timed out");

    /// <summary>AW-UIDRIVER-005: the token is missing or wrong.</summary>
    internal static readonly ErrorDefinition Unauthorized = new("AW-UIDRIVER-005", ErrorKind.Forbidden, "Unauthorized");

    /// <summary>AW-UIDRIVER-006: no driver answers (no driver.json, a stale one, a refused connection).</summary>
    internal static readonly ErrorDefinition Unreachable = new("AW-UIDRIVER-006", ErrorKind.Unavailable, "UI driver unreachable");

    /// <summary>AW-UIDRIVER-007: the app failed while the driver acted (an exception on the UI thread, a screenshot that did not render).</summary>
    internal static readonly ErrorDefinition Failed = new("AW-UIDRIVER-007", ErrorKind.Unavailable, "UI driver action failed");

    /// <summary>Every UI driver error, for ErrorCatalogue.Validate.</summary>
    internal static IReadOnlyList<ErrorDefinition> All { get; } = [Invalid, NoMatch, NotActionable, TimedOut, Unauthorized, Unreachable, Failed];
}
