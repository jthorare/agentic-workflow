namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>
/// The service tile (<see cref="ServiceTileViewModel"/>, continued): token first or the OAuth app (#4357). A service with a key opens on the token field and its three steps; GitHub and Linear offer
/// "Use an OAuth app instead", which shows the client id (and secret) fields, and "Use a token instead" goes back. Every swap drops what was typed or pasted for the other way.
/// </summary>
internal sealed partial class ServiceTileViewModel
{
    // True once the OAuth app was picked on a service that also takes a token.
    private bool OAuthPicked { get; set; }

    // True when the failed try left its input whole, so the same press can run again (#4368): a refused token is gone, so the button goes back to "Add token", never a greyed "Try again".
    private bool CanRetry { get; set; }

    /// <summary>True when Connect runs the browser sign-in: a service with no key, or one whose OAuth app was picked.</summary>
    public bool UsesOAuth => !Service.TakesKey || OAuthPicked;

    /// <summary>True when the service takes a token and also has a browser sign-in to swap to.</summary>
    public bool CanUseOAuth => Service.TakesKey && Service.Mode != ServiceTileMode.Token;

    /// <summary>True when "Use an OAuth app instead" shows: the tile asks for a token and could sign in instead.</summary>
    public bool ShowUseOAuth => IsAsking && CanUseOAuth && !UsesOAuth;

    /// <summary>True when "Use a token instead" shows: the tile asks for the OAuth app of a service that takes a token.</summary>
    public bool ShowUseToken => IsAsking && CanUseOAuth && UsesOAuth;

    // True when the client ID is what is missing and the help line under the field already asks for it (#4396), so the inline reason would say it twice. Keyed on the help line's wording (Labels.HelpAsksForClientId, #4486):
    // a help line that says something else leaves the inline line, the only one saying what is missing.
    private bool HelpSaysClientId => NeedsClientId && ClientId.Trim().Length == 0 && Labels.HelpAsksForClientId(HelpText);

    /// <summary>The numbered steps above the token field.</summary>
    public IReadOnlyList<TokenStep> TokenSteps => Labels.TileTokenSteps(Id);

    /// <summary>True when the steps show: the tile asks for a token.</summary>
    public bool ShowTokenSteps => IsAsking && TakesToken && TokenSteps.Count > 0;

    /// <summary>True when the one help line shows (the OAuth app's; a token has its steps instead).</summary>
    public bool HasHelpText => HelpText.Length > 0;

    /// <summary>Swaps to the OAuth app's fields.</summary>
    internal void UseOAuth() => Pick(oauth: true);

    /// <summary>Swaps back to the token field.</summary>
    internal void UseToken() => Pick(oauth: false);

    // Picks the way to connect and drops anything typed or pasted for either way.
    private void Pick(bool oauth)
    {
        _ = Token.Take();
        _ = ClientSecret.Take();
        (OAuthPicked, ClientId, ErrorText) = (oauth && CanUseOAuth, string.Empty, string.Empty);
        Raise(nameof(UsesOAuth), nameof(NeedsClientId), nameof(NeedsSecret), nameof(TakesToken), nameof(ConnectText), nameof(PrimaryText), nameof(BlockedReason), nameof(HasBlockedReason), nameof(CanSend), nameof(CanPress), nameof(HelpText), nameof(HelpLinkText), nameof(HelpUrl), nameof(HasHelpLink));
        RaiseAuth();
    }

    // Everything the swap links, the steps and the help line read.
    private void RaiseAuth() => Raise(nameof(ShowUseOAuth), nameof(ShowUseToken), nameof(ShowTokenSteps), nameof(HasHelpText));
}
