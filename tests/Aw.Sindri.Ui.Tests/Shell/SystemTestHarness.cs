using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>A health view model fed directly with a status, the catalog and the services view, as the follow would (System page tests).</summary>
internal sealed class SystemTestHarness
{
    private readonly Uri _api = A.Record().Url;

    internal SystemTestHarness(string servicesScenario = "approved")
    {
        Server = new FakeSindriApi { ServiceView = RouteAnswer.Api("services", servicesScenario) };
        Vm = new HealthViewModel(new InMemoryEndpointRecords(), Server, new FakeTimeProvider(A.Noon), new FakeClipboard());
    }

    internal FakeSindriApi Server { get; }

    internal HealthViewModel Vm { get; }

    /// <summary>Shows a status, then the catalog and the services as the fake serves them.</summary>
    /// <param name="status">The status, or a healthy running one.</param>
    /// <returns>A task that completes when the three are shown.</returns>
    internal async Task Show(StatusView? status = null)
    {
        var token = TestContext.Current.CancellationToken;
        Vm.Show(_api, status ?? A.Status());
        Vm.ShowCatalog(_api, (await Server.Commands(_api, token)).Value);
        await Refresh();
    }

    /// <summary>Reads the services again, as the follow does after every event.</summary>
    /// <returns>A task that completes when they are shown.</returns>
    internal async Task Refresh() => Vm.ShowViews((await Server.Services(_api, TestContext.Current.CancellationToken)).Value);
}
