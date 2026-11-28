using Aw.Kernel;
using Aw.Sindri.Ui.Api.Connections;

namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>The service tile (<see cref="ServiceTileViewModel"/>, continued): the body of the add call.</summary>
internal sealed partial class ServiceTileViewModel
{
    // The token is read, not taken: a check that never judged it (SND-CONN-019, SND-CONN-020) leaves it in the box for Try again (#4428); Run forgets it once the add lands or the key is refused.
    private AddConnectionBody Body()
    {
        var name = Service.Id + ":" + DefaultLabel;
        return TakesToken
            ? new AddConnectionBody(name, Service.Id, Kind: "rest", Client: null, KeyEnv: null, Scopes: [], Wait: false, Key: Token.HasValue ? Token.Text : null)
            : new AddConnectionBody(name, Service.Id, Kind: "oauth", ClientId.Trim(), KeyEnv: null, Scopes: [], Wait: false, ClientSecret: ClientSecret.Take());
    }

    // Forgets the token unless the check never judged it (SND-CONN-019 not reached, SND-CONN-020 rate limited); true when it stays.
    private bool KeepToken(Result<ConnectionAdded> added)
    {
        var unjudged = added.IsError && (string.Equals(added.Error.Code, "SND-CONN-019", StringComparison.Ordinal) || string.Equals(added.Error.Code, "SND-CONN-020", StringComparison.Ordinal));
        if (!unjudged)
        {
            _ = Token.Take();
        }

        return unjudged;
    }
}
