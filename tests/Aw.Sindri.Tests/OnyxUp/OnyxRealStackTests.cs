using Aw.Kernel.Services;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxDown;
using Aw.Sindri.Features.OnyxStatus;
using Aw.Sindri.Features.OnyxUp;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.OnyxUp;

/// <summary>
/// The real thing, once: `onyx up` on this box's Docker pulls the pinned images and starts the stack, `/api/version` answers v4.9.0, nginx is published on 127.0.0.1 only, a
/// second `up` does nothing, `down` stops it. Heavy: it pulls about 2.7 GB of images and starts Onyx, so it skips when Docker is missing, when the disk is short, and it is not run by
/// the tasks that wrote it (the orchestrator runs it, under a heavy-job slot). Its cleanup removes the stack and its volumes; that is the test's own, not a product behavior.
/// </summary>
public sealed class OnyxRealStackTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "heavy")]
    public async Task Up_down_cycle_on_the_real_docker()
    {
        var home = Path.Combine(Path.GetTempPath(), "aw-onyx-real-" + Guid.NewGuid().ToString("N"));
        string? Environment(string name) => string.Equals(name, "AW_SINDRI_HOME", StringComparison.Ordinal) || string.Equals(name, "AW_STATE_DIR", StringComparison.Ordinal) ? home : null;
        // The host's own composition (what Program.cs runs), so this cannot drift from main; only the progress sink is swapped, to keep the console quiet.
        await using var provider = Composition.Compose(new ServiceCollection(), Environment, TimeProvider.System)
            .AddSingleton<IOnyxProgress>(new CapturingOnyxProgress())
            .BuildServiceProvider();
        var docker = provider.GetRequiredService<IContainerRuntime>().Inspect(Token);
        Assert.SkipWhen(!docker.CliFound || !docker.DaemonAnswers, "Docker isn't available on this box");
        Assert.SkipWhen(provider.GetRequiredService<IHostProbes>().FreeDisk(Path.GetTempPath()).FreeBytes < provider.GetRequiredService<IOnyxPinsSource>().Pins.DiskBytes, "not enough free disk for the Onyx images");
        var paths = OnyxPaths.For(home);
        // Set before `up` runs, so a start that fails halfway still has its stack removed below.
        var created = paths.Project.Name;
        try
        {
            await Cycle(provider, paths);
        }
        finally
        {
            // Only the project this test's own home names is removed: the name is per home (never the owner's), and it is checked to be one Sindri makes.
            if (ComposeProject.IsOurs(created))
            {
                RemoveStack(created, paths);
            }

            if (Directory.Exists(home))
            {
                Directory.Delete(home, recursive: true);
            }
        }
    }

    private static async Task Cycle(ServiceProvider provider, OnyxPaths paths)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var runtime = services.GetRequiredService<IContainerRuntime>();
        var up = (await services.GetRequiredService<IService<OnyxUpRequest, OnyxUpResponse>>().Handle(new OnyxUpRequest(), Token)).Value;
        var recorded = services.GetRequiredService<IOnyxWorkspace>().ReadState()?.Project;
        Assert.Equal(paths.Project.Name, recorded);   // the project this home started: per home, never a shared name
        using var http = new HttpClient();

        Assert.Contains("v4.9.0", await http.GetStringAsync(new Uri(up.Url + OnyxRoutes.Version), Token), StringComparison.Ordinal);
        Assert.All(runtime.Ps(paths.Project, Token).Value.SelectMany(static c => c.Publishers), static p => Assert.Equal("127.0.0.1", p.Address));
        Assert.Equal(CheckState.Ok, (await services.GetServices<IDoctorChecks>().OfType<OnyxStackDoctor>().Single().Check(Token))!.State);
        var again = (await services.GetRequiredService<IService<OnyxUpRequest, OnyxUpResponse>>().Handle(new OnyxUpRequest(), Token)).Value;
        Assert.Equal(["fetch", "config", "pull", "start"], again.Skipped, StringComparer.Ordinal);

        _ = (await services.GetRequiredService<IService<OnyxDownRequest, OnyxDownResponse>>().Handle(new OnyxDownRequest(), Token)).Value;
        Assert.DoesNotContain(runtime.Ps(paths.Project, Token).Value, static c => c.State == "running");
        var status = (await services.GetRequiredService<IService<OnyxStatusRequest, OnyxStatusResponse>>().Handle(new OnyxStatusRequest(), Token)).Value;
        Assert.Equal(OnyxState.Stopped, status.State);
    }

    // `compose down -v` with the home's files when they all landed; after a start that failed before they did, the project's containers and volumes are found by its label.
    private static void RemoveStack(string project, OnyxPaths paths)
    {
        var runner = new ProcessToolRunner();
        var limit = TimeSpan.FromMinutes(5);
        if (File.Exists(paths.Compose) && File.Exists(paths.Override) && File.Exists(paths.Env))
        {
            _ = runner.Run("docker", ["compose", "-p", project, "-f", paths.Compose, "-f", paths.Override, "--env-file", paths.Env, "down", "-v"], limit, cancellationToken: CancellationToken.None);
            return;
        }

        var label = "label=com.docker.compose.project=" + project;
        var containers = runner.Run("docker", ["ps", "-aq", "--filter", label], limit, cancellationToken: CancellationToken.None).Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (containers.Length > 0)
        {
            _ = runner.Run("docker", ["rm", "-f", "-v", .. containers], limit, cancellationToken: CancellationToken.None);
        }

        var volumes = runner.Run("docker", ["volume", "ls", "-q", "--filter", label], limit, cancellationToken: CancellationToken.None).Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (volumes.Length > 0)
        {
            _ = runner.Run("docker", ["volume", "rm", .. volumes], limit, cancellationToken: CancellationToken.None);
        }
    }
}
