using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>
/// The one switch that turns the sandbox on (sandbox spec §12 SB-5a, SB12): on Linux the bubblewrap sandbox, its owner-copy login source and the concurrent-session cap
/// replace the 'UnavailableSandbox' Core registers; everywhere else nothing changes and every sandboxed launch is refused with SND-LAUNCH-014.
/// </summary>
public static class PlatformSandboxServiceCollectionExtensions
{
    /// <summary>The slice every Step's systemd scope lives in, below the user manager's cgroup directory (the cap counts '*.scope' directories there).</summary>
    private const string SliceCgroupPath = "sindri.slice/sindri-steps.slice";

    /// <summary>Registers the product's sandbox for this platform.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriPlatformSandbox(this IServiceCollection services, Func<string, string?> environment)
        => services.AddSindriPlatformSandbox(environment, OperatingSystem.IsLinux(), OwnUid.Read());

    internal static IServiceCollection AddSindriPlatformSandbox(this IServiceCollection services, Func<string, string?> environment, bool linux, int uid)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);
        if (!linux)
        {
            return services;
        }

        _ = services.RemoveAll<ISandbox>().RemoveAll<ISandboxFixture>().AddSingleton<IAgentTokenSource>(sp => new GitHubAppTokenSource(environment, GitHubAppTokenSource.CreateClient(), sp.GetRequiredService<TimeProvider>()));   // the fixture-plan self-check replaces Features' NoSandboxFixture; the agent-app token mint
        return services
            .AddSindriBubblewrapSandbox(environment)
            .AddSingleton<ISliceScopes>(new CgroupSliceScopes(SliceDirectory(uid)))
            .AddSingleton<ISessionCap>(sp => new FileSessionCap(LockFile(sp.GetRequiredService<SindriSettings>().Home), sp.GetRequiredService<ISliceScopes>()))
            .AddSingleton<ISandboxFixture>(sp => new LinuxSandboxFixture(sp.GetRequiredService<SindriSettings>().Home, environment, sp.GetRequiredService<ISandboxProcesses>(), sp.GetRequiredService<ISandboxMachine>()))
            .AddSingleton<SystemEgressNetwork>()
            .AddSingleton(sp => new EgressService(
                sp.GetRequiredService<SystemEgressNetwork>(), sp.GetRequiredService<SystemEgressNetwork>(), environment, EgressLogDirectory(sp.GetRequiredService<SindriSettings>().Home)))
            .AddSingleton<LaunchSandboxRegistry>()
            .AddSingleton<ISandboxLaunchStages>(sp => new SandboxLaunchStages(
                sp.GetRequiredService<IApprovedProfile>(), sp.GetRequiredService<ILearnHistory>(), [.. sp.GetServices<IModelCredentialSource>()], sp.GetRequiredService<ISandboxLaunchInputs>(),
                sp.GetRequiredService<LaunchSandboxRegistry>(), sp.GetRequiredService<TimeProvider>(), new AgentTokenRenewal(sp.GetRequiredService<IAgentTokenSource>(), sp.GetRequiredService<TimeProvider>(), static message => Console.Error.WriteLine("{\"level\":\"warn\",\"source\":\"sindri.agent-token\",\"message\":\"" + System.Text.Encodings.Web.JavaScriptEncoder.Default.Encode(message) + "\"}"))))   // the JSON line serve's stderr carries, as the egress warns
            .AddSingleton<ISandboxLaunchInputs>(sp => new LinuxSandboxLaunchInputs(
                sp.GetRequiredService<LauncherOptions>(), sp.GetRequiredService<ReloadingAgentCatalog>(), sp.GetRequiredService<ISandboxProcesses>(),
                () => BoxHostFacts.Read(environment, sp.GetRequiredService<SindriSettings>().Home, sp.GetRequiredService<ISandboxMachine>()), ControlFor(sp),
                new DenialCounterParts(
                    new ScopedSandboxDenials(async (denied, cancellationToken) =>
                    {
                        var scope = sp.CreateAsyncScope();   // LaunchSessionEvents is scoped (it holds the lease), as the launcher's EndLive
                        try
                        {
                            return await scope.ServiceProvider.GetRequiredService<LaunchSessionEvents>().Append(denied, cancellationToken).ConfigureAwait(false);
                        }
                        finally
                        {
                            await scope.DisposeAsync().ConfigureAwait(false);
                        }
                    }),
                    sp.GetRequiredService<System.Text.Json.JsonSerializerOptions>(), sp.GetRequiredService<ITextScrubber>(), sp.GetRequiredService<TimeProvider>())))
            .AddSandboxVerbs(environment)
            .AddSingleton<ISandboxLaunch>(sp => new SandboxLaunch(sp.GetRequiredService<ISandbox>(), sp.GetRequiredService<ISessionCap>(), sp.GetRequiredService<ISandboxLaunchStages>(), LaunchOptions(sp, environment)));
    }

    // The human-only verbs' ports: 'sandbox exec' and 'sandbox login'.
    private static IServiceCollection AddSandboxVerbs(this IServiceCollection services, Func<string, string?> environment) => services
        .AddSingleton<ISandboxExec>(sp => ExecFor(sp, environment))
        .AddSingleton<ISandboxLogin>(sp => new SandboxLoginRunner(sp.GetRequiredService<SindriSettings>().Home, environment));

    private static SandboxExec ExecFor(IServiceProvider sp, Func<string, string?> environment)
    {
        var machine = sp.GetRequiredService<ISandboxMachine>();
        return new SandboxExec(
            sp.GetRequiredService<ISandbox>(), sp.GetRequiredService<ISessionCap>(), sp.GetRequiredService<EgressService>(), sp.GetRequiredService<IApprovedProfile>(),
            new BoxFacts(machine.RamBytes, machine.Cores), environment("AW_SANDBOX_SYSTEMD_RUN") is { Length: > 0 } run ? run : "systemd-run",
            () => sp.GetRequiredService<LauncherOptions>().LaunchDirectory);   // why: lazily, so a container without the launcher can still resolve the exec
    }

    private static SandboxLaunchOptions LaunchOptions(IServiceProvider sp, Func<string, string?> environment)
    {
        var settings = sp.GetRequiredService<SindriSettings>();
        var machine = sp.GetRequiredService<ISandboxMachine>();
        return new SandboxLaunchOptions(
            environment("AW_SANDBOX_SYSTEMD_RUN") is { Length: > 0 } run ? run : "systemd-run",
            new BoxFacts(machine.RamBytes, machine.Cores),   // one source for machine memory: doctor's limits row and the scope limits agree
            settings.Home,
            sp.GetRequiredService<LauncherOptions>().SecretsDirectory,
            ControlFor(sp));
    }

    /// <summary>Gets the reader of the running control plane's endpoint record (control.json), shared by the launch options and the inputs.</summary>
    private static Func<ControlEndpoint?> ControlFor(IServiceProvider sp)
    {
        var path = sp.GetRequiredService<SindriSettings>().EndpointPath;
        var json = sp.GetRequiredService<System.Text.Json.JsonSerializerOptions>();
        return () => ReadEndpoint(path, json);
    }

    /// <summary>Reads control.json (a tiny local file) as FileEndpoint does; a missing or damaged record is none.</summary>
    private static ControlEndpoint? ReadEndpoint(string path, System.Text.Json.JsonSerializerOptions json)
    {
        try
        {
            var read = Kernel.Json.AwJson.Deserialize<ControlEndpoint>(File.ReadAllBytes(path), json);
            return read.IsError ? null : read.Value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Gets the cgroup directory whose '*.scope' children are the live Step sessions.</summary>
    /// <param name="uid">The real uid.</param>
    /// <returns>The directory.</returns>
    internal static string SliceDirectory(int uid) => "/sys/fs/cgroup/" + SliceRelativePath(uid);

    /// <summary>Gets the slice's cgroup path under the cgroup root (what ICgroupStats takes, with the scope's name added).</summary>
    /// <param name="uid">The real uid.</param>
    /// <returns>The path, with no leading slash.</returns>
    internal static string SliceRelativePath(int uid)
    {
        var id = uid.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return $"user.slice/user-{id}.slice/user@{id}.service/{SliceCgroupPath}";
    }

    /// <summary>Gets where the egress proxies write egress.log (one directory for the service; the log is a count per host, port and verdict).</summary>
    /// <param name="sindriHome">$AW_SINDRI_HOME.</param>
    /// <returns>'&lt;home&gt;/sandbox/egress'.</returns>
    internal static string EgressLogDirectory(string sindriHome) => Path.Combine(sindriHome, "sandbox", "egress");

    /// <summary>Gets the lock file the session cap takes, under Sindri's real home.</summary>
    /// <param name="sindriHome">$AW_SINDRI_HOME.</param>
    /// <returns>'&lt;home&gt;/locks/sessions.lock'.</returns>
    internal static string LockFile(string sindriHome) => Path.Combine(sindriHome, "locks", "sessions.lock");
}
