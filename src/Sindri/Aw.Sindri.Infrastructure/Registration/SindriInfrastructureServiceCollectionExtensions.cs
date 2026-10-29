using System.Text.Json;
using Aw.Kernel.Ids;
using Aw.Redaction;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.BugFix;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.EngineReads;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Leftovers;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Locks;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Core.ModelHistory;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Shipped;
using Aw.Sindri.Core.Workflow.Stats;
using Aw.Sindri.Core.Worktrees;
using Aw.Sindri.Infrastructure.Auth;
using Aw.Sindri.Infrastructure.BugFix;
using Aw.Sindri.Infrastructure.Challenges;
using Aw.Sindri.Infrastructure.Checks;
using Aw.Sindri.Infrastructure.Control;
using Aw.Sindri.Infrastructure.Deploy;
using Aw.Sindri.Infrastructure.Evidence;
using Aw.Sindri.Infrastructure.Forge;
using Aw.Sindri.Infrastructure.Judge;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Lease;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Leftovers;
using Aw.Sindri.Infrastructure.LensPacks;
using Aw.Sindri.Infrastructure.Line;
using Aw.Sindri.Infrastructure.Locks;
using Aw.Sindri.Infrastructure.Mcp;
using Aw.Sindri.Infrastructure.Notifications;
using Aw.Sindri.Infrastructure.Packets;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Infrastructure.Prove;
using Aw.Sindri.Infrastructure.Sandbox;
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
        return services.AddSingleton(settings).AddSindriSessions(environment, settings).AddSindriLauncher(environment, settings).AddSindriAdapters(environment).AddAgentRuntimeAdapters(environment).AddSindriMcp(environment).AddSecretInfrastructure().AddLeftoverInfrastructure().AddScopingInfrastructure(environment).AddBugFixInfrastructure(environment).AddJudgeInfrastructure(environment).AddScopeWatch();
    }

    /// <summary>SB-3a: what 'session wrap --limits' watches a session's systemd scope through (systemctl --user and the cgroup files).</summary>
    private static IServiceCollection AddScopeWatch(this IServiceCollection services) =>
        services.AddSingleton<IScopeControl>(static _ => new SystemctlScopeControl(new SandboxProcesses())).AddSingleton<ICgroupStats, CgroupFiles>().AddSingleton<IHeavyLockSweep>(static sp => new HeavyLockSweep(sp.GetRequiredService<TimeProvider>()))
            .AddSingleton<IDenialFiles>(static sp => new DiskDenialFiles(sp.GetRequiredService<LauncherOptions>().LaunchDirectory, sp.GetRequiredService<JsonSerializerOptions>()))   // SB-4b: the launcher's denials folder
            .AddSingleton<ISessionSandboxes>(static sp => new DiskSessionSandboxes(
                sp.GetRequiredService<LauncherOptions>().LaunchDirectory, sp.GetRequiredService<JsonSerializerOptions>(), sp.GetRequiredService<ICgroupStats>(), Sandbox.Launch.PlatformSandboxServiceCollectionExtensions.SliceRelativePath(OwnUid.Read())));   // SB-6a: a session row's sandbox

    /// <summary>Engine P8: the diff signal of an agent run (git) and an agent session's end and spend (the ledger).</summary>
    private static IServiceCollection AddAgentRuntimeAdapters(this IServiceCollection services, Func<string, string?> environment) =>
        services.AddSingleton<IRunDiff, GitRunDiff>().AddSingleton<IRunBranches, GitRunBranches>().AddSingleton<IWorkspaceRebase, GitWorkspaceRebase>().AddSingleton<IDiffContent, GitDiffContent>().AddSingleton<IBranchChanges, GitBranchChanges>().AddSingleton<IAgentSessions, LedgerAgentSessions>().AddSingleton<IRunSuspensions, LedgerRunSuspensions>().AddSingleton<ILearnHistory, LedgerLearnHistory>().AddSingleton<ILaunchOwners, LedgerLaunchOwners>().AddSingleton<IScopeRunRecords, ScopeRunRecords>()
            .AddSingleton<IPrepareSource, GitPrepareSource>()
            .AddSingleton<IPrepareCache>(sp => new FilePrepareCache(Path.Combine(sp.GetRequiredService<ISindriHome>().Path, "cache", "prepare"), new ProcessToolRunner()))   // <real home>/cache/prepare/<key>/ (sandbox spec 3.8)
            .AddSingleton<ICheckCommands>(sp => new ToolCheckCommands(new ProcessToolRunner(), sp.GetRequiredService<ITextScrubber>(), sp.GetRequiredService<TimeProvider>()))
            .AddLensPackAdapters(environment);

    /// <summary>LN-PK lite: the built-in lens and style packs (and the machine packs under the state root) and the review loop's changed paths.</summary>
    private static IServiceCollection AddLensPackAdapters(this IServiceCollection services, Func<string, string?> environment) =>
        services.AddSingleton<ILensPackSource, EmbeddedLensPackSource>().AddSingleton<IOutsidePackFiles, DiskPackFiles>().AddSingleton<IPackDirectories, DiskPackDirectories>().AddSingleton<ILensPacks>(sp => new ProfileLensPacks(sp.GetRequiredService<IApprovedProfile>(), sp.GetRequiredService<ILensPackSource>(), sp.GetRequiredService<IOutsidePackFiles>(), Path.Combine(SettingsFromEnvironment.StateRoot(environment), "packs")))
            .AddSingleton<IStylePackSource, EmbeddedStylePackSource>().AddSingleton<IStylePacks, ProfileStylePacks>().AddSingleton<IPackBundles, PackBundles>().AddSingleton<IChangedPaths, GitChangedPaths>();

    /// <summary>The bugfix block Steps' port over the bugfix-state program (deps.yaml Sindri to BugFix, kind cli).</summary>
    private static IServiceCollection AddBugFixInfrastructure(this IServiceCollection services, Func<string, string?> environment) =>
        services.AddSingleton<IBugFixState>(_ => new BugFixStateTool(new ProcessToolRunner(), environment));

    /// <summary>The judge Steps' port over the judge program (deps.yaml Sindri to Judge, kind cli); the judge's store is Sindri's own, under its home.</summary>
    private static IServiceCollection AddJudgeInfrastructure(this IServiceCollection services, Func<string, string?> environment) =>
        services.AddSingleton<IJudgeChoice>(sp => new JudgeCliChoice(new ProcessToolRunner(), environment, Path.Combine(sp.GetRequiredService<SindriSettings>().Home, "judge")))
            .AddSingleton<IModelPickJudge>(sp => new JudgeCliModelPick(new ProcessToolRunner(), environment, Path.Combine(sp.GetRequiredService<SindriSettings>().Home, "judge")));

    private static IServiceCollection AddScopingInfrastructure(this IServiceCollection services, Func<string, string?> environment) =>
        services.AddSingleton<IPackets>(_ => new AwPacketsTool(new ProcessToolRunner(), environment))
            .AddSingleton<IForgeRepos>(sp => new GitForgeRepos(new ProcessToolRunner(), sp.GetRequiredService<IApprovedProfile>()))
            .AddSingleton<IScopePacketStore>(sp => new ScopePacketStore(sp.GetRequiredService<SindriSettings>(), sp.GetRequiredService<IPackets>())).AddSingleton<IBriefFiles, BriefFiles>().AddSingleton<IRunArea, DiskRunArea>().AddSingleton<IPacketFiles, DiskPacketFiles>().AddSingleton<IHoldoutStore, DiskHoldoutStore>().AddSingleton<ICommitText, GitCommitText>().AddSingleton<IArtifactFiles, DiskArtifactFiles>().AddSingleton<IPacketDirectory, DiskPacketDirectory>()
            .AddSingleton<IOwnerDecisions, PendingOwnerDecisions>()
            .AddSingleton<IFrozenPackets>(sp => new FrozenPacketRecords(Path.Combine(sp.GetRequiredService<SindriSettings>().StateDirectory, "packets")))
            .AddSingleton<ITrackerSource>(_ => new FileTrackerSource(environment("AW_SINDRI_TRACKER_DIR"))).AddSingleton<IChangeSource, GitChangeSource>();

    /// <summary>The recipe store, approvals, claims, the worktree and script adapters the blocks use, and the PR comment marks.</summary>
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
            .AddSingleton<IWorktrees, GitWorktrees>()               // per-run and phase worktrees (engine spec §6.5); nothing calls it until BS-10
            .AddSingleton<IPrCommentMarks, SqlitePrCommentMarks>(); // the PR babysitter's comment marks (W26): pr_comment_marks in ledger.db

    private static IServiceCollection AddSindriAdapters(this IServiceCollection services, Func<string, string?> environment)
    {
        return services
            .AddSingleton<IIdGenerator, GuidV7Ids>().AddSingleton<IProcessProbe, PosixProcessProbe>()
            .AddSingleton<LedgerDatabase>()
            .AddSingleton<ILedgerStore, SqliteLedger>()
            .AddSingleton<ILedgerReader, SqliteLedgerReader>()
            .AddSingleton<IMirroredSequences, SqliteMirroredSequences>()   // API P13: which ledger row mirrors a Temporal event
            .AddSingleton<ILease, SqliteLease>()
            .AddSingleton<ViewsDatabase>()
            .AddSingleton<IViewStore, SqliteViewStore>()
            .AddSingleton<IViewReader, SqliteViewReader>()
            .AddSingleton<IRecipeStatsCache, SqliteRecipeStatsCache>()
            .AddSingleton<IModelHistory, SqliteModelHistory>().AddSingleton<IModelPickSettings, ProfileModelPickSettings>().AddSingleton<IModelPickHistory, ModelPickHistoryAdapter>()   // the pick's ports read the approved profile and the model-history view
            .AddSingleton<IJobViews, SqliteJobViews>().AddSingleton<IServicePauses, ViewServicePauses>()
            .AddScoped<IRecipeStateCatalog, RecipeStateCatalog>()
            .AddSingleton<IProfileSource>(sp => new YamlProfileSource(sp.GetRequiredService<SindriSettings>(), sp.GetRequiredService<JsonSerializerOptions>(), () => HeavyCap.Read(environment, sp.GetRequiredService<SindriSettings>().StateDirectory)))
            .AddSingleton<IApprovals, FileApprovals>()
            .AddSingleton<IBoxLimitsSource>(sp => new HeavyCapSource(environment, sp.GetRequiredService<SindriSettings>().StateDirectory))
            .AddSingleton<ILineSetupProbe>(new FileLineSetupProbe(environment))   // 'line doctor' (LN-L)
            .AddSingleton<IApprovedProfile>(static sp => sp.GetRequiredService<IApprovals>())
            .AddSingleton<IEndpointFile, FileEndpoint>()
            .AddSingleton<IControlPlaneProbe, HttpControlPlaneProbe>()
            .AddSingleton<IControlPlaneLauncher, ServeLauncher>()
            .AddSingleton<IHumanTokenStore, FileHumanToken>()
            .AddSingleton(static sp => new ProverLabel(sp.GetRequiredService<SindriSettings>().ProverLabelAllowed))
            .AddDoctorChecks(environment)
            .AddSingleton<IPreCommitGit, GitPreCommit>()   // cutover §6.2: the scrub verb, the onboarding hook slot and the doctor read the hook through it
            .AddSingleton<IRecipeBuild, BuildInfoRecipeBuild>()   // setup-once §5.3: doctor compares the embedded recipe set's tree hash with HEAD's
            .AddSingleton<IBuildStamps>(static _ => new InstalledBuildStamps(AppContext.BaseDirectory))   // #1032: status and doctor compare the running build with bin/cs/current
            .AddSingleton<IProcessAncestry, PosixProcessAncestry>()
            .AddSingleton<IRepoLocator, GitRepoLocator>().AddSingleton<IRepoInspector, GitRepoInspector>().AddSingleton<IProfileRepoWriter, YamlProfileRepoWriter>().AddSingleton<IProfileSandboxWriter, YamlProfileSandboxWriter>().AddSingleton<IProfileInitializer, FileProfileInitializer>()   // setup-once §5.1: AddRepo's folder checks and the profile's repos[] appender
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

    /// <summary>The doctor's adapter-backed check sets.</summary>
    private static IServiceCollection AddDoctorChecks(this IServiceCollection services, Func<string, string?> environment)
        => services
            .AddScoped<IDoctorChecks, TokenFileDoctorChecks>()
            .AddScoped<IDoctorChecks, DeployDoctorChecks>()
            .AddScoped<IDoctorChecks>(_ => new UiEvidenceDoctorChecks(environment, Environment.ProcessPath, new ProcessToolRunner()))   // cutover §3.1: `setup:ui-evidence`
            .AddScoped<IDoctorChecks>(static _ => new BannerDoctorChecks(OperatingSystem.IsMacOS(), static () => NotifierCommand.OnPath("terminal-notifier")));   // #1111: `setup:banners`

    /// <summary>The claude CLI and the providers' MCP config files, with their settings.</summary>
    private static IServiceCollection AddSindriMcp(this IServiceCollection services, Func<string, string?> environment)
        => services
            .AddSingleton(McpSettings.Read(environment))
            .AddSingleton<IClaudeCli, ProcessClaudeCli>()
            .AddSingleton<IMcpAutoSwitch>(new EnvironmentMcpSwitch(environment))
            .AddSingleton<IProviderHost, ProviderHost>()
            .AddSingleton<IProviderFiles, ProviderConfigFiles>();

    // What an old `onyx up` left on the box (docker by compose label, one keychain entry): `onyx down --purge` and the doctor's leftover row.
    private static IServiceCollection AddLeftoverInfrastructure(this IServiceCollection services) => services
        .AddSingleton<IPurgeDocker>(static _ => new ToolPurgeDocker(new ProcessToolRunner()))
        .AddSingleton<IPurgeKeychain>(static _ => new ToolPurgeKeychain(new ProcessToolRunner()))
        .AddSingleton<ILeftoverFiles, DiskLeftoverFiles>();

    private static IServiceCollection AddSecretInfrastructure(this IServiceCollection services)
        => services
            .AddSingleton<ISindriHome, SettingsSindriHome>()
            .AddSingleton<ISecretWriter>(static sp => new KeychainSecretWriter(new ProcessToolRunner(), sp.GetRequiredService<ISindriHome>()))   // the key into the OS store, else an owner-only file
            .AddSingleton<ISourceTokenProfileWriter, YamlSourceTokenProfileWriter>()
            .AddSingleton<ISecretPrompts, TerminalSecretPrompts>();                                       // the owner at the terminal, no echo for the token                      // the source pointer line in the live profile

    /// <summary>
    /// Makes the secret-pointer resolver (env:, file:, keychain:, op:) over <paramref name="environment"/>, for Composition to
    /// build before the container exists (context spec §4 "Secrets"); AddSindriInfrastructure registers the same kind.
    /// </summary>
    /// <param name="environment">Reads an env: pointer's variable. Nothing else is read from it.</param>
    /// <returns>The resolver.</returns>
    public static ISecretPointers CreateSecretPointers(Func<string, string?> environment)
        => new PointerSecrets(environment, new ProcessToolRunner());
}
