using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Sandbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>The registration the SB-5a switch calls: it must resolve with what the Host already has (the settings and a clock).</summary>
[Lane(TestLane.Unit)]
public sealed class BubblewrapRegistrationTests
{
    [Fact]
    public void Bubblewrap_registration_resolves_the_sandbox_and_both_credential_sources()
    {
        var services = new ServiceCollection()
            .AddSingleton(new SindriSettings("/home/owner/.agentic-workflow/sindri-app", "/home/owner/.agentic-workflow/sindri-app/profile.yaml"))
            .AddSingleton<TimeProvider>(new FakeTimeProvider())
            .AddSindriBubblewrapSandbox(static name => string.Equals(name, "HOME", StringComparison.Ordinal) ? "/home/owner" : null);
        using var provider = services.BuildServiceProvider();

        var sandbox = provider.GetRequiredService<ISandbox>();
        var sources = provider.GetServices<IModelCredentialSource>().ToList();

        _ = Assert.IsType<BubblewrapSandbox>(sandbox);
        _ = Assert.IsType<OwnerCopyCredentialSource>(sources[0]);
        _ = Assert.IsType<SeparateLoginCredentialSource>(sources[1]);
        Assert.Equal([ModelLoginSource.OwnerCopy, ModelLoginSource.SeparateLogin], sources.Select(x => x.Id));
        Assert.Same(provider.GetRequiredService<ILaunchSecrets>(), provider.GetRequiredService<LaunchSecretRegistry>());
    }
}
