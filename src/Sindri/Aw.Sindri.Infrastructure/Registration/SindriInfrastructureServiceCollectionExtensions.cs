using System.Text.Json;
using Aw.Kernel.Ids;
using Aw.Redaction;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.EngineReads;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Docs;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Leftovers;
using Aw.Sindri.Core.Locks;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Core.Memory;
using Aw.Sindri.Core.ModelHistory;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Worktrees;
using Aw.Sindri.Infrastructure.Auth;
using Aw.Sindri.Infrastructure.Challenges;
using Aw.Sindri.Infrastructure.Checks;
using Aw.Sindri.Infrastructure.Connections;
using Aw.Sindri.Infrastructure.Connections.Polling;
using Aw.Sindri.Infrastructure.Control;
using Aw.Sindri.Infrastructure.Deploy;
using Aw.Sindri.Infrastructure.Evidence;
using Aw.Sindri.Infrastructure.Forge;
using Aw.Sindri.Infrastructure.Judge;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Lease;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Leftovers;
using Aw.Sindri.Infrastructure.Locks;
using Aw.Sindri.Infrastructure.Mcp;
using Aw.Sindri.Infrastructure.Memory;
using Aw.Sindri.Infrastructure.Notifications;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Infrastructure.Resources;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Infrastructure.Sessions;
using Aw.Sindri.Infrastructure.Setup;
using Aw.Sindri.Infrastructure.Skills;
using Aw.Sindri.Infrastructure.Text;
using Aw.Sindri.Infrastructure.Toolkits;
using Aw.Sindri.Infrastructure.Views;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Infrastructure.Workflow.Code;
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
        return services.AddSingleton(settings).AddSindriSessions(environment, settings).AddSindriLauncher(environment, settings).AddSindriAdapters(environment).AddAgentRuntimeAdapters().AddSindriMcp(environment).AddSecretInfrastructure().AddProfileEditing(environment).AddLeftoverInfrastructure().AddScopingInfrastructure().AddJudgeInfrastructure(environment).AddScopeWatch().AddSkillBlocks(environment).AddSingleton<Core.Reviews.IReviewSummaries, Reviews.FileReviewSummaries>().AddSingleton<Core.Reviews.IFixTaskFiles, Reviews.FixTaskFiles>().AddSingleton<Core.Reviews.IFixSessions>(sp => new Reviews.GitFixSessions(sp.GetRequiredService<Core.Sessions.ISessionSnapshot>(), new ProcessToolRunner())).AddSingleton<Core.Reviews.IReviewPages, Reviews.FileReviewPages>().AddSingleton<Core.Reviews.IReviewEvidence>(_ => new Reviews.FileReviewEvidence(environment)).AddSingleton(_ => new Todos.NoteLinkCounter(new SandboxProcesses())).AddSingleton<Core.Todos.Note.ITodoNoteDisk>(sp => new Todos.FileTodoNoteDisk(sp.GetRequiredService<Todos.NoteLinkCounter>())).AddSingleton<Core.Todos.Note.ITodoNoteFiles>(sp => new Todos.FileTodoNoteFiles(sp.GetRequiredService<ISindriHome>(), sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<Todos.NoteLinkCounter>()));
    }

    /// <summary>SB-3a: what 'session wrap --limits' watches a session's systemd scope through (systemctl --user and the cgroup files).</summary>
    private static IServiceCollection AddScopeWatch(this IServiceCollection services) => services.AddScopeWatch(OperatingSystem.IsMacOS());

    /// <summary>The scope watch with its platform chosen by <paramref name="mac"/>: systemd and cgroup files, or (macOS) the process-group files.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="mac">Whether this is macOS.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddScopeWatch(this IServiceCollection services, bool mac) =>
        services.AddScopePorts(mac).AddSingleton<IHeavyLockSweep>(static sp => new HeavyLockSweep(sp.GetRequiredService<TimeProvider>()))
            .AddSingleton<IDenialFiles>(static sp => new DiskDenialFiles(sp.GetRequiredService<LauncherOptions>().LaunchDirectory, sp.GetRequiredService<JsonSerializerOptions>()))   // SB-4b: the launcher's denials folder
            .AddSingleton<ISessionSandboxes>(static sp => new DiskSessionSandboxes(
                sp.GetRequiredService<LauncherOptions>().LaunchDirectory, sp.GetRequiredService<JsonSerializerOptions>(), sp.GetRequiredService<ICgroupStats>(), PlatformSandboxServiceCollectionExtensions.SliceRelativePath(OwnUid.Read())));   // SB-6a: a session row's sandbox

    /// <summary>systemd and cgroup files on Linux; on macOS (SB-9c) the process group's files, one object for the scope control and the memory kills.</summary>
    private static IServiceCollection AddScopePorts(this IServiceCollection services, bool mac) => mac
        ? services.AddSingleton(static sp => new ProcessGroupScopeControl(ProcessGroupFiles.Directory(sp.GetRequiredService<LauncherOptions>().LaunchDirectory)))
            .AddSingleton<IScopeControl>(static sp => sp.GetRequiredService<ProcessGroupScopeControl>())
            .AddSingleton<IMemoryKills>(static sp => sp.GetRequiredService<ProcessGroupScopeControl>())
            .AddSingleton<IScopeRelease>(static sp => sp.GetRequiredService<ProcessGroupScopeControl>())
            .AddSingleton<ICgroupStats>(static _ => new ProcessGroupStats(new SandboxProcesses(), footprint: new LibprocPhysFootprint()))
        : services.AddSingleton<IScopeControl>(static _ => new SystemctlScopeControl(new SandboxProcesses())).AddSingleton<ICgroupStats, CgroupFiles>();

    /// <summary>Engine P8: the diff signal of an agent run (git) and an agent session's end and spend (the ledger).</summary>
    private static IServiceCollection AddAgentRuntimeAdapters(this IServiceCollection services) =>
        services.AddSingleton<IRunDiff, GitRunDiff>().AddSingleton<IRunBranches, GitRunBranches>().AddSingleton<IWorkspaceRebase, GitWorkspaceRebase>().AddSingleton<IDiffContent, GitDiffContent>().AddSingleton<IBranchChanges, GitBranchChanges>().AddSingleton<IAgentSessions, LedgerAgentSessions>().AddSingleton<IRunSuspensions, LedgerRunSuspensions>().AddSingleton<ILearnHistory, LedgerLearnHistory>().AddSingleton<ILaunchOwners, LedgerLaunchOwners>()
            .AddSingleton<IPrepareSource, GitPrepareSource>()
            .AddSingleton<IPrepareCache>(sp => new FilePrepareCache(Path.Combine(sp.GetRequiredService<ISindriHome>().Path, "cache", "prepare"), new ProcessToolRunner()))   // <real home>/cache/prepare/<key>/ (sandbox spec 3.8)
            .AddSingleton<ICheckCommands>(sp => new ToolCheckCommands(new ProcessToolRunner(), sp.GetRequiredService<ITextScrubber>(), sp.GetRequiredService<TimeProvider>()));

    /// <summary>The judge Steps' port over the judge program (deps.yaml Sindri to Judge, kind cli); the judge's store is Sindri's own, under its home.</summary>
    private static IServiceCollection AddJudgeInfrastructure(this IServiceCollection services, Func<string, string?> environment) =>
        services.AddSingleton<IJudgeChoice>(sp => new JudgeCliChoice(new ProcessToolRunner(), environment, Path.Combine(sp.GetRequiredService<SindriSettings>().Home, "judge")))
            .AddSingleton<IModelPickJudge>(sp => new JudgeCliModelPick(new ProcessToolRunner(), environment, Path.Combine(sp.GetRequiredService<SindriSettings>().Home, "judge")));

    private static IServiceCollection AddScopingInfrastructure(this IServiceCollection services) =>
        services.AddSingleton<IForgeRepos>(sp => new GitForgeRepos(new ProcessToolRunner(), sp.GetRequiredService<IApprovedProfile>()))
            .AddSingleton<IRunArea, DiskRunArea>();

    /// <summary>The secret pointers, the worktree and tracked-file adapters, and the workflow graph store.</summary>
    private static IServiceCollection AddRecipeAdapters(this IServiceCollection services, Func<string, string?> environment) => services
            .AddSingleton(CreateSecretPointers(environment))
            .AddSingleton<ITrackedFiles, GitTrackedFiles>()
            .AddSingleton<IWorktrees, GitWorktrees>()               // per-run worktrees (engine spec §6.5)
            .AddSingleton<IWorkflowStore, SqliteWorkflowStore>()    // workflow graphs and their revisions (#2258, PR 5): workflow and workflow_revision in ledger.db
            .AddSingleton<IWorkflowRunStore, SqliteWorkflowRunStore>()    // workflow runs and node runs (#2258, PR 6): workflow_run and workflow_node_run in ledger.db
            .AddSingleton<IWorkflowDocs, WorkflowDocs.ShippedWorkflowDocs>()   // workflow_docs: the pages embedded in the binary
            .AddSingleton<IGraphLaunchRuns, NoGraphLaunchRuns>()   // no graph run launches an agent session until PR 9
            .AddSingleton<IGraphBlockCatalog, EmptyGraphBlockCatalog>() // no graph blocks until PR 6 and 9
            .AddSingleton<IResourceFiles>(static sp => new ResourceFiles(Path.Combine(sp.GetRequiredService<ISindriHome>().Path, "resources")))   // provisioned resources (agent-first PR 14): the files under <home>/resources
            .AddSingleton(static sp => new ResourceProvisioner(new ResourceStore(Path.Combine(sp.GetRequiredService<ISindriHome>().Path, "resources")), sp.GetRequiredService<LedgerDatabase>(), sp.GetRequiredService<TimeProvider>()))
            .AddSingleton<IResourceProvisioner>(static sp => sp.GetRequiredService<ResourceProvisioner>())   // makes an owner's resources exist before a run and books them in ledger.db
            .AddSingleton<IMemoryStore, SqliteMemoryStore>()       // memory stores and facts (agent-first PR 13): memory and memory_fact in ledger.db
            .AddSingleton<IToolkitCalls, SqliteToolkitCalls>()       // toolkit call log (agent-first PR 16): toolkit_call in ledger.db
            .AddSingleton<ToolkitCallLog>()
            .AddSingleton<IGraphSeenStore, SqliteSeenStore>()      // what 'only_new' nodes have passed on (agent-first PR 9): workflow_seen in ledger.db
            .AddSingleton<IGraphHttpPort>(static sp => new GraphHttpClient(GraphHttpHandler.Create(), ct => GraphHttpClient.HostsOf(sp.GetRequiredService<IApprovedProfile>(), ct)))   // the 'http' block's client: only 'workflow.http.hosts'
            .AddSingleton<ICodeCompiler, CodeCompiler>().AddSingleton<ICodeCheck, CodeChecker>()   // the 'code' block's Roslyn compiler (agent-first PR 9c)
            .AddSingleton(static sp => new CompiledCodeCache(sp.GetRequiredService<ICodeCompiler>(), Path.Combine(sp.GetRequiredService<SindriSettings>().Home, "code-cache"), sp.GetRequiredService<TimeProvider>()))   // compiled assemblies by sha256 of the code and the reference set
            .AddSingleton<ICodeRunPort>(sp => new SandboxedCodeRunner(
                sp.GetRequiredService<CompiledCodeCache>(), sp.GetRequiredService<IApprovedProfile>(), sp.GetRequiredService<ISandbox>(),
                () => BoxHostFacts.Read(environment, sp.GetRequiredService<SindriSettings>().Home, sp.GetRequiredService<ISandboxMachine>()),
                Path.Combine(sp.GetRequiredService<SindriSettings>().Home, "code-runs"), Path.Combine(AppContext.BaseDirectory, "sindri-code-runner")))   // runs the runner beside 'sindri' in the process sandbox
            .AddConnections();

    private static IServiceCollection AddSindriAdapters(this IServiceCollection services, Func<string, string?> environment)
    {
        return services
            .AddSingleton<IIdGenerator, GuidV7Ids>().AddSingleton<IProcessProbe, PosixProcessProbe>()
            .AddSingleton<LedgerDatabase>().AddSingleton<IStoreCheckpoints>(static sp => new SqliteStoreCheckpoints([sp.GetRequiredService<SindriSettings>().LedgerPath, sp.GetRequiredService<SindriSettings>().ViewsPath]))   // #2221: the tick that truncates both logs
            .AddSingleton<ILedgerStore, SqliteLedger>()
            .AddSingleton<ILedgerReader, SqliteLedgerReader>()
            .AddSingleton<IMirroredSequences, SqliteMirroredSequences>()   // API P13: which ledger row mirrors a Temporal event
            .AddSingleton<ILease, SqliteLease>()
            .AddSingleton<ViewsDatabase>()
            .AddSingleton<IViewStore, SqliteViewStore>()
            .AddSingleton<IViewReader, SqliteViewReader>()
            .AddSingleton<IModelHistory, SqliteModelHistory>().AddSingleton<IModelPickSettings, ProfileModelPickSettings>().AddSingleton<IModelPickHistory, ModelPickHistoryAdapter>()   // the pick's ports read the approved profile and the model-history view
            .AddSingleton<IJobViews, SqliteJobViews>().AddSingleton<IServicePauses, ViewServicePauses>()
            .AddSingleton<IProfileSource>(sp => new YamlProfileSource(sp.GetRequiredService<SindriSettings>(), sp.GetRequiredService<JsonSerializerOptions>(), () => HeavyCap.Read(environment, sp.GetRequiredService<SindriSettings>().StateDirectory)))
            .AddSingleton<IProfileFile>(static sp => new SettingsProfileFile(sp.GetRequiredService<SindriSettings>()))   // #3113: a profile file the source can't read is not a missing one
            .AddSingleton<IOldApprovalsCleanup, ApprovalsDirectoryCleanup>()   // #2258 PR 1: run once at start by serve
            .AddSingleton<IBoxLimitsSource>(sp => new HeavyCapSource(environment, sp.GetRequiredService<SindriSettings>().StateDirectory))
            .AddSingleton(static sp => new LiveApprovedProfile(sp.GetRequiredService<IProfileSource>(), sp.GetRequiredService<SindriSettings>().ProfilePath)).AddSingleton<IApprovedProfile>(static sp => sp.GetRequiredService<LiveApprovedProfile>()).AddSingleton<IProfileCache>(static sp => sp.GetRequiredService<LiveApprovedProfile>())
            .AddSingleton<IEndpointFile, FileEndpoint>()
            .AddSingleton<IControlPlaneProbe, HttpControlPlaneProbe>()
            .AddSingleton<IControlPlaneLauncher, ServeLauncher>()
            .AddSingleton<IServeRelauncher>(static sp => new ServeRelauncher(sp.GetRequiredService<SindriSettings>(), AppContext.BaseDirectory))   // #3052: restart onto bin/cs/current
            .AddSingleton<IHumanTokenStore, FileHumanToken>()
            .AddSingleton(static sp => new ProverLabel(sp.GetRequiredService<SindriSettings>().ProverLabelAllowed))
            .AddDoctorChecks(environment)
            .AddSingleton<IPreCommitGit, GitPreCommit>()   // cutover §6.2: the scrub verb, the onboarding hook slot and the doctor read the hook through it
            .AddSingleton<IBuildStamps>(static _ => new InstalledBuildStamps(AppContext.BaseDirectory))   // #1032: status and doctor compare the running build with bin/cs/current
            .AddSingleton<IProcessAncestry, PosixProcessAncestry>()
            .AddSingleton<IRepoLocator, GitRepoLocator>().AddSingleton<IRepoInspector, GitRepoInspector>().AddSingleton<IRepoCheckouts, FileRepoCheckouts>().AddSingleton<IProfileRepoWriter, YamlProfileRepoWriter>().AddSingleton<IProfileIndexWriter>(sp => new YamlProfileIndexWriter(sp.GetRequiredService<SindriSettings>(), sp.GetRequiredService<JsonSerializerOptions>(), sp.GetRequiredService<IProfileCache>(), () => HeavyCap.Read(environment, sp.GetRequiredService<SindriSettings>().StateDirectory))).AddSingleton<IProfileSandboxWriter, YamlProfileSandboxWriter>().AddSingleton<IProfileInitializer, FileProfileInitializer>()   // setup-once §5.1: AddRepo's folder checks and the profile's repos[] appender
            .AddSingleton<ICommandOrigins, LedgerCommandOrigins>()   // engine P4 unit (d): the bridge's causation route
            .AddRecipeAdapters(environment)
            .AddSingleton<IChallenges, ProcessChallenges>()
            .AddSingleton(sp => HeavyLockSettings.Read(environment, sp.GetRequiredService<SindriSettings>().StateDirectory))
            .AddSingleton<IHeavySlots, DirectoryHeavySlots>()
            .AddToolkitInfrastructure(environment)
            .AddSingleton<IHarnessVersions, CommandHarnessVersions>()
            .AddSingleton<IOsNotifier, OsascriptNotifier>()          // engine P6 unit (c): the only OS notifier (README X5, X50)
            .AddSingleton<IDeskNotices, FileDeskNotices>()           // engine P6 unit (c): notifications.jsonl
            .AddSingleton<ITextScrubber>(new RedactingScrubber(Redactor.Default))
            .AddSingleton<ISecretFinder>(new RedactingSecretFinder(Redactor.Default));
    }

    /// <summary>The doctor's adapter-backed check sets.</summary>
    private static IServiceCollection AddDoctorChecks(this IServiceCollection services, Func<string, string?> environment)
        => services
            .AddScoped<IDoctorChecks, TokenFileDoctorChecks>()
            .AddScoped<IDoctorChecks, DeployDoctorChecks>()
            .AddScoped<IDoctorChecks>(_ => new UiEvidenceDoctorChecks(environment, Environment.ProcessPath, new ProcessToolRunner()))   // cutover §3.1: `setup:ui-evidence`
            .AddScoped<IDoctorChecks>(static sp => new ResourceDoctorChecks(sp.GetRequiredService<IResourceFiles>(), sp.GetRequiredService<ResourceProvisioner>()))   // agent-first PR 14: the `resources` row
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
        .AddSingleton<ILeftoverFiles, DiskLeftoverFiles>()
        .AddSingleton<Core.Linear.ILinearCheckStore, Linear.FileLinearCheckStore>();   // what the last save found out about each Linear key, so Settings' read needs no key lookup

    // 'profile set' (#1740): the in-place key writer and the reader that checks its result before it is written.
    private static IServiceCollection AddProfileEditing(this IServiceCollection services, Func<string, string?> environment)
        => services
            .AddSingleton<IProfileKeyWriter, YamlProfileKeyWriter>()
            .AddSingleton<IProfileTextParser>(sp => new YamlProfileTextParser(sp.GetRequiredService<JsonSerializerOptions>(), () => HeavyCap.Read(environment, sp.GetRequiredService<SindriSettings>().StateDirectory)));

    // Connections (PR 10a): the store, the keychain secrets, the presets, the loopback sign-in and the token client.
    private static IServiceCollection AddConnections(this IServiceCollection services)
        => services
            .AddSingleton<IConnectionStore, SqliteConnectionStore>()
            .AddSingleton<IConnectionSecrets>(static sp => new KeychainConnectionSecrets(sp.GetRequiredService<ISecretWriter>(), sp.GetRequiredService<ISecretPointers>(), new ProcessToolRunner()))
            .AddSingleton<IConnectionPresetCatalog, EmbeddedConnectionPresets>()
            .AddSingleton<IEnvironmentReader, ProcessEnvironmentReader>()
            .AddSingleton<IConnectionProbe>(static _ => new HttpConnectionProbe(new HttpClient()))
            .AddSingleton<IOAuthClient>(static sp => new HttpOAuthClient(new HttpClient(), sp.GetRequiredService<TimeProvider>()))
            .AddSingleton<IMcpConnectionClient>(static _ => new McpConnectionClient(new HttpClient()))
            .AddSingleton<IConnectionToolPort>(static sp => new ConnectionToolAdapter(sp.GetRequiredService<IConnectionStore>(), sp.GetRequiredService<IConnectionSecrets>(), sp.GetRequiredService<IMcpConnectionClient>(), NoRedirectClient(), sp.GetRequiredService<ITextScrubber>()))
            .AddSingleton<IBrowserOpener>(static _ => new BrowserOpener())
            .AddSingleton<IConnectionCredentials>(static sp => new ConnectionCredentials(sp.GetRequiredService<IConnectionStore>(), sp.GetRequiredService<IConnectionSecrets>(), sp.GetRequiredService<ISecretPointers>()))   // #2508: the one token order (connection first, profile pointer second) for the github, slack and linear feeds
            .AddSingleton<IGitHubAccount>(static sp => new Connections.GitHub.GitHubConnectionAccount(sp.GetRequiredService<IConnectionStore>(), sp.GetRequiredService<IConnectionSecrets>(), sp.GetRequiredService<IConnectionPresetCatalog>(), NoRedirectClient(), sp.GetRequiredService<TimeProvider>()))   // #2499: the Reviews tab's GitHub account, from the Connections page
            .AddSingleton<IPollCursors, SqlitePollCursors>()   // PR 11: poll cursors in ledger.db
            .AddSingleton<IEventPoller>(static sp => new GitHubEventPoller(NoRedirectClient(), sp.GetRequiredService<IConnectionSecrets>(), sp.GetRequiredService<TimeProvider>()))
            .AddSingleton<IEventPoller>(static sp => new LinearEventPoller(NoRedirectClient(), sp.GetRequiredService<IConnectionSecrets>(), sp.GetRequiredService<TimeProvider>()))
            .AddSingleton<IEventPoller>(static sp => new SlackEventPoller(NoRedirectClient(), sp.GetRequiredService<IConnectionSecrets>(), sp.GetRequiredService<TimeProvider>()))
            .AddSingleton<IEventPoller>(static sp => new GoogleCalendarPoller(NoRedirectClient(), sp.GetRequiredService<IConnectionSecrets>(), sp.GetRequiredService<TimeProvider>()))
            .AddSingleton<ISignInSession>(static sp => new LoopbackSignIn(sp.GetRequiredService<IOAuthClient>(), sp.GetRequiredService<IBrowserOpener>(), TimeSpan.FromMinutes(5), System.Security.Cryptography.RandomNumberGenerator.GetBytes));

    // A poller sends the connection's token, so its client never follows a redirect on its own: the poller refuses one that leaves the preset's host.
    private static HttpClient NoRedirectClient() => new(_noRedirect, disposeHandler: false);

    private static readonly SocketsHttpHandler _noRedirect = new() { AllowAutoRedirect = false };

    private static IServiceCollection AddSecretInfrastructure(this IServiceCollection services)
        => services
            .AddSingleton<ISindriHome, SettingsSindriHome>()
            .AddSingleton<ISecretWriter>(static sp => new KeychainSecretWriter(new ProcessToolRunner(), sp.GetRequiredService<ISindriHome>()))   // the key into the OS store, else an owner-only file
            .AddSingleton<YamlSourceTokenProfileWriter>()
            .AddSingleton<ISourceTokenProfileWriter>(static sp => sp.GetRequiredService<YamlSourceTokenProfileWriter>())
            .AddSingleton<ILinearWriteTokenWriter>(static sp => sp.GetRequiredService<YamlSourceTokenProfileWriter>())
            .AddSingleton<ILinearProfileEditCheck>(static sp => sp.GetRequiredService<YamlSourceTokenProfileWriter>())
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
