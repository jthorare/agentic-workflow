using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>SB12, SB-5a: the published product Host registers bubblewrap on Linux and only UnavailableSandbox elsewhere, with no test type.</summary>
public sealed class ProductHostSandboxTests
{
    private static IServiceCollection Compose() =>
        Composition.Compose(new ServiceCollection(), name => name == "AW_STATE_DIR" ? Path.Combine(Path.GetTempPath(), "aw-sindri-sb1-di") : null, new FakeTimeProvider());

    [Fact, Trait("Lane", "unit")]
    public void Product_host_registers_the_sandbox_the_platform_has()
    {
        var services = Compose();

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ISandbox));
        using var provider = services.BuildServiceProvider();
        if (OperatingSystem.IsLinux())
        {
            Assert.Null(descriptor.ImplementationType);   // the bubblewrap factory
            _ = Assert.IsType<BubblewrapSandbox>(provider.GetRequiredService<ISandbox>());
            _ = Assert.IsType<FileSessionCap>(provider.GetRequiredService<ISessionCap>());
        }
        else
        {
            Assert.Equal(typeof(UnavailableSandbox), descriptor.ImplementationType);
            _ = Assert.IsType<UnavailableSandbox>(provider.GetRequiredService<ISandbox>());
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Product_host_names_no_type_from_the_testing_assembly_and_does_not_reference_it()
    {
        var services = Compose();

        Assert.DoesNotContain(
            services,
            d => (d.ImplementationType ?? d.ImplementationInstance?.GetType() ?? d.ServiceType).Assembly.GetName().Name!.StartsWith("Aw.Sindri.Testing", StringComparison.Ordinal));
        Assert.DoesNotContain(typeof(Composition).Assembly.GetReferencedAssemblies(), a => a.Name!.Contains("Aw.Sindri.Testing", StringComparison.Ordinal));
        Assert.DoesNotContain(services, d => string.Equals(d.ImplementationType?.Name, "PassThroughSandbox", StringComparison.Ordinal) || string.Equals(d.ImplementationType?.Name, "RecordingSandbox", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_credential_registry_has_the_owner_copy_and_separate_login_sources_on_Linux_only()
    {
        using var provider = Compose().BuildServiceProvider();

        var sources = provider.GetRequiredService<ModelCredentialSources>();

        Assert.Equal(OperatingSystem.IsLinux(), sources.Has(ModelLoginSource.OwnerCopy));
        Assert.Equal(OperatingSystem.IsLinux(), sources.Has(ModelLoginSource.SeparateLogin));
    }

    [Fact, Trait("Lane", "unit")]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    public void On_Linux_the_product_container_resolves_the_sandbox_step_the_launcher_is_given()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the sandbox step is registered on Linux only");
        using var provider = Compose().BuildServiceProvider();

        // LauncherParts is private to the launcher registration, which reads exactly this: Sandbox = sp.GetService<ISandboxLaunch>().
        var step = provider.GetService<ISandboxLaunch>();

        _ = Assert.IsType<SandboxLaunch>(step);
        _ = provider.GetRequiredService<ILauncher>();   // the launcher itself builds with it
    }
}
