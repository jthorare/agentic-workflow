using Aw.Sindri.Ui.Api.Connections;

namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>The service tile (<see cref="ServiceTileViewModel"/>, continued): the body of the add call.</summary>
internal sealed partial class ServiceTileViewModel
{
    private AddConnectionBody Body()
    {
        var name = Service.Id + ":" + DefaultLabel;
        return TakesToken
            ? new AddConnectionBody(name, Service.Id, Kind: "rest", Client: null, KeyEnv: null, Scopes: [], Wait: false, Key: Token.Take())
            : new AddConnectionBody(name, Service.Id, Kind: "oauth", ClientId.Trim(), KeyEnv: null, Scopes: [], Wait: false, ClientSecret: ClientSecret.Take());
    }
}
