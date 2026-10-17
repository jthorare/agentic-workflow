using System.Text.Json;
using Aw.Kernel.Ids;
using Aw.Redaction;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Locks;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Infrastructure.Auth;
using Aw.Sindri.Infrastructure.Challenges;
using Aw.Sindri.Infrastructure.Control;
using Aw.Sindri.Infrastructure.Lease;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Locks;
using Aw.Sindri.Infrastructure.Mcp;
using Aw.Sindri.Infrastructure.Notifications;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Infrastructure.Text;
using Aw.Sindri.Infrastructure.Views;
using Aw.Sindri.Infrastructure.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Infrastructure.Registration;

/// <summary>Registers Aw.Sindri.Infrastructure's adapters and settings. A second technology for a port gets its own AddSindri&lt;Tech&gt;Infrastructure method.</summary>
public static class SindriInfrastructureServiceCollectionExtensions
{
    /// <summary>Adds the Sindri adapters (SQLite ledger, lease and views, YAML profile and recipes, files, the process probe, process ancestry, git repo resolution, secret pointers, the Aw.Redaction text scrubber, the claude CLI and the providers' MCP files) and their settings.</summary>
    /// <param name="services">The service collection.</param>
    /// <remarks>The profile and approval adapters take the package's AwJson options, which Compose registers.</remarks>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriInfrastructure(this IServiceCollection services, Func<string, string?> environment)
        => services
            .AddSettings(environment)
            .AddSingleton<IIdGenerator, GuidV7Ids>()
            .AddSingleton<IProcessProbe, PosixProcessProbe>()
            .AddSingleton<LedgerDatabase>()
            .AddSingleton<ILedgerStore, SqliteLedger>()
            .AddSingleton<ILedgerReader, SqliteLedgerReader>()
            .AddSingleton<ILease, SqliteLease>()
            .AddSingleton<ViewsDatabase>()
            .AddSingleton<IViewStore, SqliteViewStore>()
            .AddSingleton<IViewReader, SqliteViewReader>()
            .AddSingleton<IJobViews, SqliteJobViews>().AddSingleton<IServicePauses, ViewServicePauses>()
            .AddSingleton<IProfileSource, YamlProfileSource>()
            .AddSingleton<IApprovals, FileApprovals>()
            .AddSingleton<IApprovedProfile>(static sp => sp.GetRequiredService<IApprovals>())
            .AddSingleton<IEndpointFile, FileEndpoint>()
            .AddSingleton<IControlPlaneProbe, HttpControlPlaneProbe>()
            .AddSingleton<IControlPlaneLauncher, ServeLauncher>()
            .AddSingleton<IHumanTokenStore, FileHumanToken>()
            .AddSingleton(static sp => new ProverLabel(sp.GetRequiredService<SindriSettings>().ProverLabelAllowed))
            .AddSingleton<IProcessAncestry, PosixProcessAncestry>()
            .AddSingleton<IRepoLocator, GitRepoLocator>()
            .AddSingleton<ICommandOrigins, LedgerCommandOrigins>()   // engine P4 unit (d): the bridge's causation route
            .AddSingleton<IRecipeReader, YamlRecipeReader>()
            .AddSingleton<IRecipeFiles, FileRecipeFiles>()
            .AddSingleton(CreateSecretPointers(environment))
            .AddSingleton<IRecipeWake, NoOpRecipeWake>()
            .AddSingleton<IRecipeStore, SqliteRecipeStore>()
            .AddSingleton<IRecipeApprovals, SqliteRecipeApprovals>()
            .AddSingleton<IWorkflowClaims, SqliteWorkflowClaims>()   // engine P6 unit (d): workflow_claims in ledger.db
            .AddSingleton<IClaimRefs, GitClaimRefs>()                // engine P6 unit (d): the claim's marker ref
            .AddSingleton<IChallenges, ProcessChallenges>()
            .AddSingleton(sp => HeavyLockSettings.Read(environment, sp.GetRequiredService<SindriSettings>().StateDirectory))
            .AddSingleton<IHeavySlots, DirectoryHeavySlots>()
            .AddSingleton<IHarnessVersions, CommandHarnessVersions>()
            .AddSingleton<IOsNotifier, OsascriptNotifier>()          // engine P6 unit (c): the only OS notifier (README X5, X50)
            .AddSingleton<IDeskNotices, FileDeskNotices>()           // engine P6 unit (c): notifications.jsonl
            .AddMcpAdapters(environment)
            .AddOnyxInfrastructure(environment)
            .AddSingleton<ITextScrubber>(new RedactingScrubber(Redactor.Default));

    // The settings and the one place that reads Sindri's home from them.
    private static IServiceCollection AddSettings(this IServiceCollection services, Func<string, string?> environment)
        => services
            .AddSingleton(SettingsFromEnvironment.Read(environment))
            .AddSingleton<ISindriHome, SettingsSindriHome>();

    // The MCP registration adapters: its settings, the claude CLI and the provider config files.
    private static IServiceCollection AddMcpAdapters(this IServiceCollection services, Func<string, string?> environment)
        => services
            .AddSingleton(McpSettings.Read(environment))
            .AddSingleton<IClaudeCli, ProcessClaudeCli>()
            .AddSingleton<IProviderFiles, ProviderConfigFiles>();

    private static IServiceCollection AddOnyxInfrastructure(this IServiceCollection services, Func<string, string?> environment)
        => services
            .AddSingleton<IOnyxPinsSource, EmbeddedOnyxPins>()                                            // context/onyx.lock, embedded
            .AddSingleton<IContainerRuntime>(_ => new DockerCliRuntime(new ProcessToolRunner(), environment))   // docker facts and compose operations
            .AddSingleton<IDeployFiles>(static _ => new HttpDeployFiles(new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }), new Uri("https://raw.githubusercontent.com/onyx-dot-app/onyx/")))   // the pinned files, https, no redirect; the handler keeps the environment proxy on purpose (corporate networks need it, and integrity is the pinned sha256, not the path)
            .AddSingleton<IOnyxWorkspace>(sp => new FileOnyxWorkspace(sp.GetRequiredService<ISindriHome>(), sp.GetRequiredService<JsonSerializerOptions>()))   // onyx/ under the Sindri home
            .AddSingleton<IComposeServices, YamlComposeServices>()                                       // the compose file's services, read only
            .AddSingleton<IRandomHex, OsRandomHex>()                                                      // the OS random source, for the auth key
            .AddSingleton<IOnyxHealth>(static _ => new HttpOnyxHealth(HttpOnyxHealth.CreateClient()))                 // readiness, http://127.0.0.1 only
            .AddSingleton<IHostProbes>(static _ => new HostProbes());                                     // free disk and loopback ports

    /// <summary>
    /// Makes the secret-pointer resolver (env:, file:, keychain:, op:) over <paramref name="environment"/>, for Composition to
    /// build before the container exists (context spec §4 "Secrets"); AddSindriInfrastructure registers the same kind.
    /// </summary>
    /// <param name="environment">Reads an env: pointer's variable. Nothing else is read from it.</param>
    /// <returns>The resolver.</returns>
    public static ISecretPointers CreateSecretPointers(Func<string, string?> environment)
        => new PointerSecrets(environment, new ProcessToolRunner());
}
