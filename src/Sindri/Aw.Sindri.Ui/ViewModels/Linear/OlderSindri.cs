using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>Tells an older running Sindri from one that is down (#2247): a Sindri without the Linear routes answers a bare 404, which the client reads as "answered 404".</summary>
internal static class OlderSindri
{
    /// <summary>True when the error is a bare 404 from a reachable Sindri. A 404 that carries one of Sindri's own codes (not synced, no such issue) means the route exists and is not this.</summary>
    /// <param name="error">The failed call's error.</param>
    /// <returns>True when the running Sindri has no such route.</returns>
    internal static bool IsNotServed(Error error) =>
        string.Equals(error.Code, SindriUiErrors.BadStatus.Code, StringComparison.Ordinal) && error.Message.EndsWith(" answered 404", StringComparison.Ordinal);
}
