using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>A Needs-you row (<see cref="NeedsYouRowViewModel"/>, continued): what the served open adds, the route its click goes to and the pull request it can open.</summary>
internal sealed partial class NeedsYouRowViewModel
{
    /// <summary>The row's served open.route as a link, when it parses; null when none is served or the app cannot read it (the older rule then decides).</summary>
    public DeepLink? RouteLink => Entry.Open?.Route is { Length: > 0 } route && DeepLink.TryParse(route, out var link) ? link : null;

    /// <summary>The served words of the row's open button ("Answer the question"); empty when none is served.</summary>
    public string OpenLabel => Entry.Open?.Label ?? "";

    /// <summary>The pull request the row is about, as an http or https address; null when none is served or it is not one.</summary>
    public Uri? PrUri => Entry.Open?.Pr is { Url: { } url } && Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) ? uri : null;

    /// <summary>True when the row is about a pull request that can be opened.</summary>
    public bool HasPr => PrUri is not null;

    /// <summary>"Open PR #12"; empty without a pull request.</summary>
    public string OpenPrText => HasPr && Entry.Open?.Pr is { } pr ? Labels.OpenPr(pr.Number) : "";
}
