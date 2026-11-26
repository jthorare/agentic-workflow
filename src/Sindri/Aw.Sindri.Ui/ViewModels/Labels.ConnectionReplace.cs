using Aw.Kernel;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The words of a connected key tile's Replace token (<see cref="Labels"/>, continued; #4358).</summary>
internal static partial class Labels
{
    /// <summary>The link on a connected key tile that opens the paste box.</summary>
    internal const string TileReplaceToken = "Replace token";

    /// <summary>The placeholder of the paste box.</summary>
    internal const string TileReplaceTokenLabel = "New token";

    /// <summary>The paste box's send button.</summary>
    internal const string TileReplaceSend = "Replace";

    /// <summary>The note on a tile whose new token the service did not check.</summary>
    internal const string TileReplacedUnchecked = "Token replaced, not checked";

    /// <summary>The line shown while the new token is checked.</summary>
    internal const string TileReplaceChecking = "Checking the new token...";

    /// <summary>Why a replacement did not happen: the old token stays in use. A known code is worded, never shown as a code.</summary>
    /// <param name="error">The failure.</param>
    /// <param name="service">The service's title ('GitHub').</param>
    /// <returns>The words.</returns>
    internal static string ReplaceTokenFailed(Error error, string service)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Code switch
        {
            "SND-CONN-018" => $"{service} refused the new token, so the old one is still in use. Check it was copied whole and hasn't expired, then paste it again.",
            "SND-CONN-019" => $"Couldn't reach {service} to check the new token, so the old one is still in use. Check your network, then try again.",
            "SND-CONN-020" => $"{service} is limiting requests right now, so the new token wasn't checked and the old one is still in use. Wait a minute, then try again.",
            _ => "Couldn't replace the token; the old one is still in use. " + InstanceRefused(error),
        };
    }
}
