using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>SB12: the published product Host registers only UnavailableSandbox, with no switch and no test type.</summary>
public sealed class ProductHostSandboxTests
{
    private static IServiceCollection Compose() =>
        Composition.Compose(new ServiceCollection(), name => name == "AW_STATE_DIR" ? Path.Combine(Path.GetTempPath(), "aw-sindri-sb1-di") : null, new FakeTimeProvider());

    [Fact, Trait("Lane", "unit")]
    public void Product_host_registers_only_UnavailableSandbox()
    {
        var services = Compose();

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ISandbox));
        Assert.Equal(typeof(UnavailableSandbox), descriptor.ImplementationType);
        using var provider = services.BuildServiceProvider();
        _ = Assert.IsType<UnavailableSandbox>(provider.GetRequiredService<ISandbox>());
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
    public void The_credential_registry_is_registered_and_empty_until_SB_5a_wires_the_adapter()
    {
        using var provider = Compose().BuildServiceProvider();

        var sources = provider.GetRequiredService<ModelCredentialSources>();

        Assert.False(sources.Has(ModelLoginSource.OwnerCopy));
        Assert.False(sources.Has(ModelLoginSource.SeparateLogin));
    }
}
