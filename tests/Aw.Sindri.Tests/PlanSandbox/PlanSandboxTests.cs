using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Features.PlanSandbox;
using Aw.Sindri.Host;
using Aw.Sindri.Testing.Sandbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.PlanSandbox;

/// <summary>The PlanSandbox slice: one launch planned through the registered sandbox (sandbox spec SB12).</summary>
public sealed class PlanSandboxTests
{
    private static IService<PlanSandboxRequest, PlanSandboxResponse> Service(ISandbox? replacement = null)
    {
        var services = Composition.Compose(new ServiceCollection(), name => name == "AW_STATE_DIR" ? Path.Combine(Path.GetTempPath(), "aw-sindri-sb1-slice") : null, new FakeTimeProvider());
        if (replacement is not null)
        {
            _ = services.AddSingleton(replacement);   // what a test host does: the last registration wins
        }

        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IService<PlanSandboxRequest, PlanSandboxResponse>>();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task PlanSandbox_in_a_test_host_returns_the_pass_through_plan()
    {
        var result = await Service(new PassThroughSandbox()).Handle(new PlanSandboxRequest(Req.WriteScope), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(SandboxMechanism.PassThrough, result.Value.Plan.Mechanism);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task PlanSandbox_in_the_product_host_is_refused_with_014()
    {
        var result = await Service().Handle(new PlanSandboxRequest(Req.WriteScope), TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.LaunchSandboxUnavailable.Code, result.Error.Code);
        Assert.Equal("unsupported-platform", SandboxRefusal.ReasonOf(result.Error));
    }
}
