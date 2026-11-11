using Aw.Sindri.Ui.Api.Connections;

namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>The service tile (<see cref="ServiceTileViewModel"/>, continued): the body of the add call.</summary>
internal sealed partial class ServiceTileViewModel
{
    private AddConnectionBody Body()
    {
        var name = Service.Id + ":" + DefaultLabel;
        return Service.Mode == ServiceTileMode.Token
            ? new AddConnectionBody(name, Preset: null, Kind: "rest", Client: null, KeyEnv: null, Scopes: [], Wait: false, Key: Token.Take())
            : new AddConnectionBody(name, Service.Id, Kind: null, ClientId.Trim(), KeyEnv: null, Scopes: [], Wait: false, ClientSecret: ClientSecret.Take());
    }
}
