using Aw.Forge.Core.Ports;
using Aw.Forge.Core.Refs;
using Aw.Forge.Infrastructure;
using Aw.Forge.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Forge.Tests.Contract;

/// <summary>
/// The read side of the contract against a real GitHub scratch repository. Skipped unless AW_FORGE_GITHUB_REPO (owner/name)
/// and AW_FORGE_TOKEN are both set; nothing is ever sent otherwise.
/// </summary>
[Trait("Lane", "heavy")]
public sealed class GitHubForgeContractTests : ForgeReadContract, IDisposable
{
    private ServiceProvider? _provider;

    protected override ForgeUnderTest Create()
    {
        var repo = Environment.GetEnvironmentVariable("AW_FORGE_GITHUB_REPO");
        var credential = Environment.GetEnvironmentVariable("AW_FORGE_TOKEN");
        if (string.IsNullOrWhiteSpace(repo) || string.IsNullOrWhiteSpace(credential) || repo.Split('/') is not [var owner, var name])
        {
            Assert.Skip("set AW_FORGE_GITHUB_REPO (owner/name of a scratch repository) and AW_FORGE_TOKEN to run the GitHub contract");
            throw new InvalidOperationException("skipped");   // Assert.Skip throws first; this keeps the pattern variables definitely assigned
        }

        _provider = new ServiceCollection()
            .AddForgeGitHubInfrastructure(Environment.GetEnvironmentVariable, new ForgeHosts(["github.com"]))
            .BuildServiceProvider();
        return new ForgeUnderTest(_provider.GetRequiredService<IForge>(), Seed: null, new ForgeRepo("github.com", owner, name), Requests: null, CanPush: false);
    }

    public void Dispose() => _provider?.Dispose();
}
