using Aw.BugFix.Core.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.BugFix.Infrastructure;

/// <summary>Registers Aw.BugFix.Infrastructure's adapters and settings. A second technology for a port gets its own AddBugFix&lt;Tech&gt;Infrastructure method.</summary>
public static class BugFixInfrastructureServiceCollectionExtensions
{
    /// <summary>Adds the BugFix adapters: JSON files under the state directory, the local file system, git and processes.</summary>
    /// <param name="services">The service collection (with the package's AwJson options registered).</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddBugFixInfrastructure(this IServiceCollection services, Func<string, string?> environment)
        => services
            .AddSingleton(BugFixSettings.Read(environment))
            .AddSingleton<IStateStore, FileStateStore>()
            .AddSingleton<IRunLedger, FileRunLedger>()
            .AddSingleton<IFiles, LocalFiles>()
            .AddSingleton<IGit, GitProcess>()
            .AddSingleton<ICheckRunner, ProcessCheckRunner>()
            .AddSingleton<IJudge>(_ => new JudgeCli(environment))
            .AddSingleton<IUiEvidence>(_ => new UiEvidenceCli(environment));
}
