using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Onyx;
using Aw.Context.Core.Trust;
using Aw.Context.Infrastructure;
using Aw.Context.Infrastructure.Onyx;
using Aw.Context.Testing.Map;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Onyx;

/// <summary>AddContextInfrastructure registers the factory that builds an onyx source per profile settings and teams.</summary>
public sealed class OnyxRegistrationTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_onyx_source_factory_resolves_and_builds_a_source_from_settings_and_teams()
    {
        var services = new ServiceCollection()
            .AddSingleton<TimeProvider>(new FakeTimeProvider(OnyxSourceHarness.Now))
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton(FixtureMap.Json)   // the host's AwJson options; the map reader and exports folder take them (S-3a)
            .AddContextInfrastructure(static _ => null);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        var settings = OnyxSettings.Create("http://127.0.0.1:3189", OnyxSourceHarness.Pointer, "v4.9.0").Value;

        var source = provider.GetRequiredService<OnyxSources>().Create(settings, new HashSet<string>(StringComparer.Ordinal) { "SPL" });

        Assert.Equal("onyx", source.Describe().Id.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_source_catalog_adds_the_onyx_source_after_repo_docs_when_the_profile_names_onyx_and_a_repo_is_in_scope()
    {
        var services = new ServiceCollection()
            .AddSingleton<TimeProvider>(new FakeTimeProvider(OnyxSourceHarness.Now))
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton(FixtureMap.Json)   // the host's AwJson options; the map reader and exports folder take them (S-3a)
            .AddContextInfrastructure(static name => name == "AW_SINDRI_HOME" ? "/nonexistent/sindri-home" : null);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        var catalog = provider.GetRequiredService<ISourceCatalog>();
        var scrubbers = provider.GetRequiredService<IScrubberFactory>().For([]).Value;
        var repos = new[] { new ContextRepo(ContextRepoName.From("split-panes"), "/nonexistent/split-panes") };
        var plain = new ContextSettings();
        var named = plain with
        {
            Onyx = OnyxSettings.Create("http://127.0.0.1:3189", OnyxSourceHarness.Pointer, "v4.9.0").Value,
            LinearTeams = ["SPL"],
        };

        Assert.Equal(["repo-docs"], catalog.For(plain, repos, scrubbers).Select(s => s.Describe().Id.Value), StringComparer.Ordinal);
        Assert.Equal(["repo-docs", "onyx"], catalog.For(named, repos, scrubbers).Select(s => s.Describe().Id.Value), StringComparer.Ordinal);
        Assert.Empty(catalog.For(named, [], scrubbers));   // no repo in scope, no source (SourceCatalogContract)
    }
}
