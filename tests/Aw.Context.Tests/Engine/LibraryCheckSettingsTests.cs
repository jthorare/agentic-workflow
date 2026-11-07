using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Tests.Map;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Tests.Engine;

/// <summary>The profile check applies the library's home-dependent path rules when the host knows the machine's folders (PR review page spec 2026-11-04 §5.1): validate and approve refuse what the feed would.</summary>
public sealed class LibraryCheckSettingsTests
{
    private static readonly LibraryHost _host = new() { Home = "/home/harbor", SindriHome = "/home/harbor/.agentic-workflow/sindri-app", StateDefault = "/home/harbor/.agentic-workflow" };

    private static Kernel.Result<ContextSettings> Check(string library, LibraryHost? host)
    {
        using var world = new MapWorld(configure: services =>
        {
            if (host is not null)
            {
                _ = services.AddSingleton(host);
            }
        });
        using var section = JsonDocument.Parse("{\"sources\":{\"library\":" + library + "}}");
        return world.Service<IContextEngine>().CheckSettings(section.RootElement.Clone(), []);
    }

    [Fact]
    public void A_vault_that_is_the_home_folder_is_refused_with_the_key_path()
    {
        var result = Check("""{"vaults":[{"name":"work","path":"/home/harbor"}]}""", _host);

        Assert.True(result.IsError);
        Assert.Equal(ContextErrors.LibraryInvalid.Code, result.Error.Code);
        Assert.Contains("context.sources.library.vaults[0].path", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_vault_inside_the_aw_root_is_refused()
    {
        var result = Check("""{"stateDir":"/home/harbor/state","vaults":[{"name":"work","path":"/home/harbor/state/notes"}]}""", _host);

        Assert.True(result.IsError);
        Assert.Equal(ContextErrors.LibraryInvalid.Code, result.Error.Code);
    }

    [Fact]
    public void Roots_that_pass_the_rules_are_accepted()
    {
        var result = Check("""{"vaults":[{"name":"work","path":"~/Documents/WorkVault"}]}""", _host);

        Assert.False(result.IsError);
    }

    [Fact]
    public void A_host_that_does_not_know_the_machine_checks_the_shape_only()
    {
        var result = Check("""{"vaults":[{"name":"work","path":"/home/harbor"}]}""", host: null);

        Assert.False(result.IsError);
    }
}
