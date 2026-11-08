using Aw.Context.Core;
using Aw.Context.Core.Linear;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>What the four Connections services share: who may call them, and how Linear's answers read to the owner.</summary>
internal static class LinearConnectionRules
{
    /// <summary>What a session is told on the connection read.</summary>
    internal const string ReadRefusal = "Linear's connection is the owner's to read; a session can't (fix: use the app)";

    /// <summary>What a session is told on every write.</summary>
    internal const string ChangeRefusal = "Linear's connection is the owner's to change; a session can't (fix: use the app)";

    /// <summary>What a read save with no team ticked is told.</summary>
    internal const string NoTeams = "Pick at least one team to read (fix: choose teams, then Save)";

    /// <summary>What a write key equal to the read key is told.</summary>
    internal const string SameAsRead = "The write key is the same as the read key (fix: make a second key with write access and paste that)";

    /// <summary>Whether the current caller is the owner (a person or the app).</summary>
    /// <param name="callers">The current caller.</param>
    /// <returns>True for a human or a service.</returns>
    internal static bool IsOwner(ICallerContextAccessor callers) => callers.Current?.Class is CallerClass.Human or CallerClass.Service;

    /// <summary>Words Linear's refusal and silence as the page shows them; anything else passes as it is. The key is never in a message.</summary>
    /// <param name="error">What Context's check answered.</param>
    /// <param name="write">Whether the key was pasted as the write key.</param>
    /// <returns>SND-LINEAR-004 for a refused key, SND-LINEAR-010 for Linear not answering, else the error itself.</returns>
    internal static Error Linear(Error error, bool write)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (string.Equals(error.Code, ContextErrors.LinearKeyRefused.Code, StringComparison.Ordinal))
        {
            return LinearHubErrors.WriteKeyRefused.With(write
                ? "Linear refused this key (fix: make a new key with write access at linear.app/settings/api and paste it)"
                : "Linear refused this key (fix: make a new key at linear.app/settings/api and paste it)");
        }

        return string.Equals(error.Code, ContextErrors.SourceUnavailable.Code, StringComparison.Ordinal)
            ? LinearHubErrors.Unreachable.With("Linear can't be reached right now (fix: check the network, then try again)")
            : error;
    }

    /// <summary>The refusal a session gets.</summary>
    /// <param name="text">The words.</param>
    /// <returns>SND-AUTH-002.</returns>
    internal static Error Refused(string text) => SindriErrors.AuthRefused.With(text);
}
