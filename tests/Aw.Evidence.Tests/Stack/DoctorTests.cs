using Aw.Evidence.Features.Doctor;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.Stack;

public sealed class DoctorTests
{
    private static async Task<DoctorResponse> Check(FakeStackProbe probe, string? appTitle, int? apiPort = 5002) =>
        (await new DoctorService(probe, new FakeEvidenceSettings { AppTitle = appTitle, AppTitleSource = "APP_TITLE", ApiPort = apiPort }, new InMemoryRunFiles()).Handle(new DoctorRequest(), CancellationToken.None)).Value;

    [Fact, Trait("Lane", "unit")]
    public async Task Doctor_is_healthy_when_the_app_database_and_api_answer()
    {
        var report = await Check(new FakeStackProbe { Title = "App" }, "App");

        Assert.Equal((true, 0), (report.Healthy, report.Failures.Count));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Doctor_refuses_a_foreign_app_and_lists_every_failure()
    {
        var probe = new FakeStackProbe { Title = "Someone else", DatabaseReady = false };
        probe.OpenPorts.Clear();

        var report = await Check(probe, "App");

        Assert.False(report.Healthy);
        Assert.Equal(3, report.Failures.Count);
        Assert.StartsWith(":3000 does not look like this app", report.Failures[0], StringComparison.Ordinal);
        Assert.Equal("api-server (:5002) is not accepting connections", report.Failures[2]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_an_api_port_configured_doctor_does_not_check_one()
    {
        var probe = new FakeStackProbe { Title = "App" };
        probe.OpenPorts.Clear();

        var report = await Check(probe, "App", apiPort: null);

        Assert.Equal((true, 0), (report.Healthy, report.Failures.Count));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Doctor_is_not_healthy_without_an_app_title_and_says_where_to_set_it()
    {
        var report = await Check(new FakeStackProbe(), appTitle: null);

        Assert.False(report.Healthy);
        Assert.Equal("the app title is not set: set APP_TITLE", report.Failures[0]);
    }
}
