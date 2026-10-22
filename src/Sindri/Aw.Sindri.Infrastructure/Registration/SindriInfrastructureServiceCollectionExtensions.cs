using System.Text.Json;
using Aw.Kernel.Ids;
using Aw.Redaction;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.BugFix;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Locks;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Shipped;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Worktrees;
using Aw.Sindri.Infrastructure.Auth;
using Aw.Sindri.Infrastructure.BugFix;
using Aw.Sindri.Infrastructure.Challenges;
using Aw.Sindri.Infrastructure.Checks;
using Aw.Sindri.Infrastructure.Control;
using Aw.Sindri.Infrastructure.Judge;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Lease;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.LensPacks;
using Aw.Sindri.Infrastructure.Locks;
using Aw.Sindri.Infrastructure.Mcp;
using Aw.Sindri.Infrastructure.Notifications;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Infrastructure.Packets;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Infrastructure.Prove;
using Aw.Sindri.Infrastructure.Scoping;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Infrastructure.Sessions;
using Aw.Sindri.Infrastructure.Setup;
using Aw.Sindri.Infrastructure.Text;
using Aw.Sindri.Infrastructure.Views;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Infrastructure.Worktrees;
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
    {
        var settings = SettingsFromEnvironment.Read(environment);
        return services.AddSingleton(settings).AddSindriSessions(environment, settings).AddSindriLauncher(environment, settings).AddSindriAdapters(environment).AddAgentRuntimeAdapters().AddSindriMcp(environment).AddOnyxInfrastructure(environment).AddScopingInfrastructure(environment).AddBugFixInfrastructure(environment).AddJudgeInfrastructure(environment);
    }

    /// <summary>Engine P8: the diff signal of an agent run (git) and an agent session's end and spend (the ledger).</summary>
    private static IServiceCollection AddAgentRuntimeAdapters(this IServiceCollection services) =>
        services.AddSingleton<IRunDiff, GitRunDiff>().AddSingleton<IDiffContent, GitDiffContent>().AddSingleton<IBranchChanges, GitBranchChanges>().AddSingleton<IAgentSessions, LedgerAgentSessions>()
            .AddSingleton<ICheckCommands>(sp => new ToolCheckCommands(new ProcessToolRunner(), sp.GetRequiredService<ITextScrubber>(), sp.GetRequiredService<TimeProvider>()))
            .AddSingleton<ILensPackSource, EmbeddedLensPackSource>().AddSingleton<IPackDirectories, DiskPackDirectories>().AddSingleton<ILensPacks, ProfileLensPacks>().AddSingleton<IChangedPaths, GitChangedPaths>();   // LN-PK lite: the built-in lens packs and the review loop's changed paths

    /// <summary>The bugfix block Steps' port over the bugfix-state program (deps.yaml Sindri to BugFix, kind cli).</summary>
    private static IServiceCollection AddBugFixInfrastructure(this IServiceCollection services, Func<string, string?> environment) =>
        services.AddSingleton<IBugFixState>(_ => new BugFixStateTool(new ProcessToolRunner(), environment));

    /// <summary>The judge Steps' port over the judge program (deps.yaml Sindri to Judge, kind cli); the judge's store is Sindri's own, under its home.</summary>
    private static IServiceCollection AddJudgeInfrastructure(this IServiceCollection services, Func<string, string?> environment) =>
        services.AddSingleton<IJudgeChoice>(sp => new JudgeCliChoice(new ProcessToolRunner(), environment, Path.Combine(sp.GetRequiredService<SindriSettings>().Home, "judge")));

    private static IServiceCollection AddScopingInfrastructure(this IServiceCollection services, Func<string, string?> environment) =>
        services.AddSingleton<IPackets>(_ => new AwPacketsTool(new ProcessToolRunner(), environment))
            .AddSingleton<IScopePacketStore>(sp => new ScopePacketStore(sp.GetRequiredService<SindriSettings>(), sp.GetRequiredService<IPackets>())).AddSingleton<IBriefFiles, BriefFiles>().AddSingleton<IRunArea, DiskRunArea>().AddSingleton<IPacketFiles, DiskPacketFiles>().AddSingleton<IHoldoutStore, DiskHoldoutStore>().AddSingleton<ICommitText, GitCommitText>().AddSingleton<IArtifactFiles, DiskArtifactFiles>().AddSingleton<IPacketDirectory, DiskPacketDirectory>()
            .AddSingleton<IOwnerDecisions, PendingOwnerDecisions>()
            .AddSingleton<IFrozenPackets>(sp => new FrozenPacketRecords(Path.Combine(sp.GetRequiredService<SindriSettings>().StateDirectory, "packets")))
            .AddSingleton<ITrackerSource>(_ => new FileTrackerSource(environment("AW_SINDRI_TRACKER_DIR"))).AddSingleton<IChangeSource, GitChangeSource>();

    /// <summary>The recipe store, approvals, claims and the worktree and script adapters the blocks use.</summary>
    private static IServiceCollection AddRecipeAdapters(this IServiceCollection services, Func<string, string?> environment) => services
            .AddSingleton<IRecipeReader, YamlRecipeReader>()
            .AddSingleton<IRecipeFiles, FileRecipeFiles>()
            .AddSingleton<IRecipeDirectories, FileRecipeDirectories>()
            .AddSingleton(CreateSecretPointers(environment))
            .AddSingleton<IRecipeWake, NoOpRecipeWake>()
            .AddSingleton<IRecipeStore, SqliteRecipeStore>()
            .AddSingleton<IRecipeApprovals, SqliteRecipeApprovals>()
            .AddSingleton<IPhaseStarts>(static sp => new PhaseStarts(sp.GetRequiredService<IInstanceRuntime>, sp.GetRequiredService<IRecipeApprovals>(), sp.GetRequiredService<IRecipeStore>(), sp.GetRequiredService<TimeProvider>()))   // LN-WF1: effect.line.start-phase starts the phase as a linked instance
            .AddSingleton<IWorkflowClaims, SqliteWorkflowClaims>()   // engine P6 unit (d): workflow_claims in ledger.db
            .AddSingleton<IClaimRefs, GitClaimRefs>()                // engine P6 unit (d): the claim's marker ref
            .AddSingleton<IScriptRuns>(_ => new BashScriptRuns(new ProcessToolRunner()))   // the prove station's before/after scripts
            .AddSingleton<ITrackedFiles, GitTrackedFiles>()          // the docs-sync scope's file list
            .AddSingleton<IWorktrees, GitWorktrees>();              // per-run and phase worktrees (engine spec §6.5); nothing calls it until BS-10

    private static IServiceCollection AddSindriAdapters(this IServiceCollection services, Func<string, string?> environment)
    {
        return services
            .AddSingleton<IIdGenerator, GuidV7Ids>().AddSingleton<IProcessProbe, PosixProcessProbe>()
            .AddSingleton<LedgerDatabase>()
            .AddSingleton<ILedgerStore, SqliteLedger>()
            .AddSingleton<ILedgerReader, SqliteLedgerReader>()
            .AddSingleton<ILease, SqliteLease>()
            .AddSingleton<ViewsDatabase>()
            .AddSingleton<IViewStore, SqliteViewStore>()
            .AddSingleton<IViewReader, SqliteViewReader>()
            .AddSingleton<IJobViews, SqliteJobViews>().AddSingleton<IServicePauses, ViewServicePauses>()
            .AddSingleton<IProfileSource>(sp => new YamlProfileSource(sp.GetRequiredService<SindriSettings>(), sp.GetRequiredService<JsonSerializerOptions>(), () => HeavyCap.Read(environment, sp.GetRequiredService<SindriSettings>().StateDirectory)))
            .AddSingleton<IApprovals, FileApprovals>()
            .AddSingleton<IApprovedProfile>(static sp => sp.GetRequiredService<IApprovals>())
            .AddSingleton<IEndpointFile, FileEndpoint>()
            .AddSingleton<IControlPlaneProbe, HttpControlPlaneProbe>()
            .AddSingleton<IControlPlaneLauncher, ServeLauncher>()
            .AddSingleton<IHumanTokenStore, FileHumanToken>()
            .AddSingleton(static sp => new ProverLabel(sp.GetRequiredService<SindriSettings>().ProverLabelAllowed, sp.GetRequiredService<SindriSettings>().ProverMayConfirm))
            .AddScoped<IDoctorChecks, TokenFileDoctorChecks>()
            .AddSingleton<IRecipeBuild, BuildInfoRecipeBuild>()   // setup-once §5.3: doctor compares the embedded recipe set's tree hash with HEAD's
            .AddSingleton<IProcessAncestry, PosixProcessAncestry>()
            .AddSingleton<IRepoLocator, GitRepoLocator>().AddSingleton<IRepoInspector, GitRepoInspector>().AddSingleton<IProfileRepoWriter, YamlProfileRepoWriter>().AddSingleton<IProfileInitializer, FileProfileInitializer>()   // setup-once §5.1: AddRepo's folder checks and the profile's repos[] appender
            .AddSingleton<ICommandOrigins, LedgerCommandOrigins>()   // engine P4 unit (d): the bridge's causation route
            .AddRecipeAdapters(environment)
            .AddSingleton<IChallenges, ProcessChallenges>()
            .AddSingleton(sp => HeavyLockSettings.Read(environment, sp.GetRequiredService<SindriSettings>().StateDirectory))
            .AddSingleton<IHeavySlots, DirectoryHeavySlots>()
            .AddSingleton<IHarnessVersions, CommandHarnessVersions>()
            .AddSingleton<IOsNotifier, OsascriptNotifier>()          // engine P6 unit (c): the only OS notifier (README X5, X50)
            .AddSingleton<IDeskNotices, FileDeskNotices>()           // engine P6 unit (c): notifications.jsonl
            .AddSingleton<ITextScrubber>(new RedactingScrubber(Redactor.Default));
    }

    /// <summary>The claude CLI and the providers' MCP config files, with their settings.</summary>
    private static IServiceCollection AddSindriMcp(this IServiceCollection services, Func<string, string?> environment)
        => services
            .AddSingleton(McpSettings.Read(environment))
            .AddSingleton<IClaudeCli, ProcessClaudeCli>()
            .AddSingleton<IMcpAutoSwitch>(new EnvironmentMcpSwitch(environment))
            .AddSingleton<IProviderHost, ProviderHost>()
            .AddSingleton<IProviderFiles, ProviderConfigFiles>();

    private static IServiceCollection AddOnyxInfrastructure(this IServiceCollection services, Func<string, string?> environment)
        => services
            .AddSingleton<ISindriHome, SettingsSindriHome>()
            .AddSingleton<IOnyxPinsSource, EmbeddedOnyxPins>()                                            // context/onyx.lock, embedded
            .AddSingleton<IContainerRuntime>(_ => new DockerCliRuntime(new ProcessToolRunner(), environment))   // docker facts and compose operations
            .AddSingleton<IDeployFiles>(static _ => new HttpDeployFiles(new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }), new Uri("https://raw.githubusercontent.com/onyx-dot-app/onyx/")))   // the pinned files, https, no redirect; the handler keeps the environment proxy on purpose (corporate networks need it, and integrity is the pinned sha256, not the path)
            .AddSingleton<IOnyxWorkspace>(sp => new FileOnyxWorkspace(sp.GetRequiredService<ISindriHome>(), sp.GetRequiredService<JsonSerializerOptions>()))   // onyx/ under the Sindri home
            .AddSingleton<IComposeServices, YamlComposeServices>()                                       // the compose file's services, read only
            .AddSingleton<IRandomHex, OsRandomHex>()                                                      // the OS random source, for the auth key
            .AddSingleton<IOnyxHealth>(static _ => new HttpOnyxHealth(HttpOnyxHealth.CreateClient()))                 // readiness, http://127.0.0.1 only
            .AddSingleton<IHostProbes>(static _ => new HostProbes())                                       // free disk and loopback ports
            .AddSingleton<IOnyxProvisioning, HttpOnyxProvisioning>()                                        // the setup-only requests, loopback, five on the list (spec §5.2)
            .AddSingleton<ISecretWriter>(static sp => new KeychainSecretWriter(new ProcessToolRunner(), sp.GetRequiredService<ISindriHome>()))   // the key into the OS store, else an owner-only file
            .AddSingleton<IOnyxProfileWriter, YamlOnyxProfileWriter>()                                     // context.sources.onyx in the live profile
            .AddSingleton<IOnyxPrompts, TerminalPrompts>()                                                  // the owner at the terminal, no echo for the password
            .AddSingleton<IBrowser, SystemBrowser>();                                                     // the OS opener, loopback pages only

    /// <summary>
    /// Makes the secret-pointer resolver (env:, file:, keychain:, op:) over <paramref name="environment"/>, for Composition to
    /// build before the container exists (context spec §4 "Secrets"); AddSindriInfrastructure registers the same kind.
    /// </summary>
    /// <param name="environment">Reads an env: pointer's variable. Nothing else is read from it.</param>
    /// <returns>The resolver.</returns>
    public static ISecretPointers CreateSecretPointers(Func<string, string?> environment)
        => new PointerSecrets(environment, new ProcessToolRunner());
}
