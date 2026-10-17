using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

public sealed class OnyxUpResumeTests
{
    private const string Compose = "docker_compose/docker-compose.yml";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static IEnumerable<string> Recorded(OnyxUpRig rig) => (rig.Workspace.State?.Steps.Keys ?? []).Order(StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public async Task A_hash_mismatch_stops_at_fetch_with_004_and_the_rerun_retries_fetch()
    {
        var rig = new OnyxUpRig();
        var good = rig.Remote[Compose];
        rig.Remote[Compose] = "tampered\n"u8.ToArray();

        var error = (await rig.Up(Token)).Error;

        Assert.Equal("SND-ONYX-004", error.Code);
        Assert.Equal(0, rig.Workspace.StateWrites);
        Assert.DoesNotContain("Pull", rig.Runtime.Calls, StringComparer.Ordinal);
        rig.Remote[Compose] = good;

        var rerun = (await rig.Up(Token)).Value;

        Assert.Equal(["fetch", "config", "pull", "start"], rerun.Ran, StringComparer.Ordinal);
        Assert.Equal(2, rig.Files.Fetches);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_failure_011_leaves_fetch_and_config_done_and_the_rerun_starts_at_pull()
    {
        var rig = new OnyxUpRig();
        rig.Runtime.FailNext("Pull", SindriErrors.OnyxComposeFailed.With("docker compose pull failed: no space left on device"));

        var error = (await rig.Up(Token)).Error;

        Assert.Equal("SND-ONYX-011", error.Code);
        Assert.Equal(["config", "fetch"], Recorded(rig), StringComparer.Ordinal);
        var rerun = (await rig.Up(Token)).Value;
        Assert.Equal(["pull", "start"], rerun.Ran, StringComparer.Ordinal);
        Assert.Equal(["fetch", "config"], rerun.Skipped, StringComparer.Ordinal);
        Assert.Equal(2, rig.Files.Fetches);   // verified again, nothing downloaded
        Assert.Equal(rig.Pins.Pins.Files.Count, rig.Files.Requested.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_wrong_digest_after_pull_is_004_naming_the_image()
    {
        var rig = new OnyxUpRig();
        var backend = rig.Runtime.LocalImages.Keys.Single(static k => k.Contains("onyx-backend", StringComparison.Ordinal));
        rig.Runtime.LocalImages[backend] = ["onyxdotapp/onyx-backend@sha256:" + new string('f', 64)];

        var error = (await rig.Up(Token)).Error;

        Assert.Equal("SND-ONYX-004", error.Code);
        Assert.Contains("docker.io/onyxdotapp/onyx-backend", error.Message, StringComparison.Ordinal);
        Assert.Equal(["config", "fetch"], Recorded(rig), StringComparer.Ordinal);
        Assert.DoesNotContain("Up", rig.Runtime.Calls, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_wrong_digest_of_minio_after_pull_is_004_because_the_env_enables_its_profile()
    {
        var rig = new OnyxUpRig();
        var minio = rig.Runtime.LocalImages.Keys.Single(static k => k.Contains("minio", StringComparison.Ordinal));
        rig.Runtime.LocalImages[minio] = ["onyxdotapp/minio@sha256:" + new string('f', 64)];

        var error = (await rig.Up(Token)).Error;

        Assert.Equal("SND-ONYX-004", error.Code);
        Assert.Contains("minio", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Up", rig.Runtime.Calls, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_start_timeout_leaves_pull_done_and_the_rerun_starts_at_start()
    {
        var rig = new OnyxUpRig();
        rig.Health.Default = new OnyxProbe(Healthy: false, BackendVersion: null);
        var first = rig.Up(Token);
        await WaitForProbes(rig, 1);

        rig.Clock.Advance(TimeSpan.FromMinutes(16));
        var error = (await first).Error;

        Assert.Equal("SND-ONYX-005", error.Code);
        Assert.Equal(["config", "fetch", "pull"], Recorded(rig), StringComparer.Ordinal);
        rig.Health.Default = new OnyxProbe(Healthy: true, BackendVersion: "v4.9.0");
        var rerun = (await rig.Up(Token)).Value;
        Assert.Equal(["start"], rerun.Ran, StringComparer.Ordinal);
        Assert.Equal(["fetch", "config", "pull"], rerun.Skipped, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cancel_during_the_pull_records_nothing_for_it_and_releases_the_lock()
    {
        var rig = new OnyxUpRig();
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);
        rig.Runtime.OnCall = operation =>
        {
            if (operation == "Pull")
            {
                source.Cancel();
            }
        };

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await rig.Up(source.Token));

        Assert.Equal(["config", "fetch"], Recorded(rig), StringComparer.Ordinal);
        Assert.False(rig.Workspace.LockHeld);
        rig.Runtime.OnCall = null;
        var rerun = (await rig.Up(Token)).Value;
        Assert.Equal(["pull", "start"], rerun.Ran, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_changed_lock_row_reruns_fetch_config_pull_and_start()
    {
        var rig = new OnyxUpRig();
        _ = await rig.Up(Token);
        var pins = rig.Pins.Pins;
        var changed = "#!/bin/sh\necho changed\n"u8.ToArray();
        rig.Remote["data/nginx/run-nginx.sh"] = changed;
        rig.Pins.Pins = pins with { Files = [.. pins.Files.Select(f => f.Path == "data/nginx/run-nginx.sh" ? f with { Sha256 = OnyxFilePin.Sha256Of(changed) } : f)] };

        var rerun = (await rig.Up(Token)).Value;

        Assert.Equal(["fetch", "config", "pull", "start"], rerun.Ran, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_changed_image_digest_alone_reruns_config_pull_and_start()
    {
        var rig = new OnyxUpRig();
        _ = await rig.Up(Token);
        var pins = rig.Pins.Pins;
        var nginx = pins.Images.Single(static i => i.Platform == "linux/arm64" && i.Repository.EndsWith("nginx", StringComparison.Ordinal));
        var moved = nginx with { Digest = "sha256:" + new string('a', 64) };
        rig.Pins.Pins = pins with { Images = [.. pins.Images.Select(i => i == nginx ? moved : i)] };
        rig.Runtime.LocalImages[moved.Repository + "@" + moved.Digest] = [ImageNames.WithoutDefaults(moved.Repository + "@" + moved.Digest)];

        var rerun = (await rig.Up(Token)).Value;

        Assert.Equal(["config", "pull", "start"], rerun.Ran, StringComparer.Ordinal);
        Assert.Equal(["fetch"], rerun.Skipped, StringComparer.Ordinal);
        Assert.Contains(moved.Digest, rig.Workspace.Files[rig.Paths.Override], StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_deleted_pinned_file_is_fetched_again_without_rerunning_the_later_steps()
    {
        var rig = new OnyxUpRig();
        _ = await rig.Up(Token);
        const string nginx = "data/nginx/run-nginx.sh";
        _ = rig.Files.Local.Remove(nginx);
        _ = rig.Workspace.Files.Remove(rig.Paths.DeployDir + "/" + nginx);

        var rerun = (await rig.Up(Token)).Value;

        Assert.Equal(["fetch"], rerun.Ran, StringComparer.Ordinal);
        Assert.Equal(["config", "pull", "start"], rerun.Skipped, StringComparer.Ordinal);
        Assert.Equal(2, rig.Files.Fetches);
        Assert.Contains(nginx, rig.Files.Requested.Skip(1), StringComparer.Ordinal);
        Assert.True(rig.Workspace.Files.ContainsKey(rig.Paths.DeployDir + "/" + nginx));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_tampered_compose_file_is_restored_to_its_pin()
    {
        var rig = new OnyxUpRig();
        _ = await rig.Up(Token);
        rig.Files.Local[Compose] = "tampered\n"u8.ToArray();

        var rerun = (await rig.Up(Token)).Value;

        Assert.Equal(["fetch"], rerun.Ran, StringComparer.Ordinal);
        Assert.Equal(OnyxFixtures.ReadText(Compose), rig.Workspace.Files[rig.Paths.Compose], StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_deleted_file_whose_download_fails_its_pin_is_004()
    {
        var rig = new OnyxUpRig();
        _ = await rig.Up(Token);
        _ = rig.Files.Local.Remove(Compose);
        rig.Remote[Compose] = "tampered\n"u8.ToArray();

        Assert.Equal("SND-ONYX-004", (await rig.Up(Token)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_hand_edited_override_is_rewritten_and_a_hand_edited_env_is_restored_with_its_key()
    {
        var rig = new OnyxUpRig();
        _ = await rig.Up(Token);
        var (env, over) = (rig.Workspace.Files[rig.Paths.Env], rig.Workspace.Files[rig.Paths.Override]);
        rig.Workspace.Files[rig.Paths.Override] = over + "    ports: [\"0.0.0.0:80:80\"]\n";
        rig.Workspace.Files[rig.Paths.Env] = env + "COMPOSE_PROFILES=everything\n";

        var rerun = (await rig.Up(Token)).Value;

        Assert.Equal(["config", "start"], rerun.Ran, StringComparer.Ordinal);
        Assert.Equal(over, rig.Workspace.Files[rig.Paths.Override], StringComparer.Ordinal);
        Assert.Equal(env, rig.Workspace.Files[rig.Paths.Env], StringComparer.Ordinal);
        Assert.Equal(1, rig.Random.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rerun_keeps_the_port_when_our_own_nginx_holds_it()
    {
        var rig = new OnyxUpRig();
        _ = await rig.Up(Token);
        _ = rig.Probes.TakenPorts.Add(3000);

        var rerun = (await rig.Up(Token)).Value;

        Assert.Equal(3000, rerun.Port);
        Assert.Equal(["fetch", "config", "pull", "start"], rerun.Skipped, StringComparer.Ordinal);
        Assert.Equal(3000, rig.Workspace.State!.Port);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rerun_after_a_failed_start_keeps_the_port_our_nginx_holds()
    {
        var rig = new OnyxUpRig();
        _ = await rig.Up(Token);
        var state = rig.Workspace.State!;
        rig.Workspace.State = state with { Steps = state.Steps.Where(static s => s.Key != "start").ToDictionary(static s => s.Key, static s => s.Value, StringComparer.Ordinal) };
        _ = rig.Probes.TakenPorts.Add(3000);

        var rerun = (await rig.Up(Token)).Value;

        Assert.Equal(3000, rerun.Port);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stopped_stack_keeps_its_port_even_when_the_preferred_one_is_free_again()
    {
        var rig = new OnyxUpRig();
        _ = rig.Probes.TakenPorts.Add(3000);
        rig.Runtime.PublishedPort = 3001;
        _ = await rig.Up(Token);
        _ = rig.Runtime.Stop(rig.Paths.Project, Token);
        _ = rig.Probes.TakenPorts.Remove(3000);

        var rerun = (await rig.Up(Token)).Value;

        Assert.Equal(3001, rerun.Port);
        Assert.Equal(3001, rig.Workspace.State!.Port);
        Assert.Equal(["start"], rerun.Ran, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stopped_stack_moves_only_when_something_else_holds_its_port()
    {
        var rig = new OnyxUpRig();
        _ = rig.Probes.TakenPorts.Add(3000);
        rig.Runtime.PublishedPort = 3001;
        _ = await rig.Up(Token);
        _ = rig.Runtime.Stop(rig.Paths.Project, Token);
        _ = rig.Probes.TakenPorts.Remove(3000);
        _ = rig.Probes.TakenPorts.Add(3001);

        var rerun = (await rig.Up(Token)).Value;

        Assert.Equal(3000, rerun.Port);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_corrupt_state_with_a_running_stack_keeps_the_port_our_nginx_holds()
    {
        var rig = new OnyxUpRig();
        _ = await rig.Up(Token);
        _ = rig.Probes.TakenPorts.Add(3000);
        rig.Workspace.StateIsCorrupt = true;

        var rerun = (await rig.Up(Token)).Value;

        Assert.Equal(3000, rerun.Port);
        Assert.Equal(3000, rig.Workspace.State!.Port);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_taken_port_that_is_not_ours_is_not_kept()
    {
        var rig = new OnyxUpRig();
        _ = await rig.Up(Token);
        _ = rig.Runtime.Stop(rig.Paths.Project, Token);
        _ = rig.Probes.TakenPorts.Add(3000);

        var rerun = (await rig.Up(Token)).Value;

        Assert.Equal(3001, rerun.Port);
        Assert.Contains("start", rerun.Ran, StringComparer.Ordinal);
        Assert.Contains("config", rerun.Ran, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stopped_stack_with_done_steps_only_runs_start()
    {
        var rig = new OnyxUpRig();
        _ = await rig.Up(Token);
        _ = rig.Runtime.Stop(rig.Paths.Project, Token);

        var rerun = (await rig.Up(Token)).Value;

        Assert.Equal(["start"], rerun.Ran, StringComparer.Ordinal);
        Assert.Equal(["fetch", "config", "pull"], rerun.Skipped, StringComparer.Ordinal);
        Assert.Equal(2, rig.Runtime.Calls.Count(static c => c == "Up"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lost_auth_key_in_env_is_regenerated_once_and_kept()
    {
        var rig = new OnyxUpRig();
        _ = await rig.Up(Token);
        _ = rig.Workspace.Files.Remove(rig.Paths.Env);

        var second = (await rig.Up(Token)).Value;
        var regenerated = OnyxConfig.ReadSecret(rig.Workspace.Files[rig.Paths.Env]);
        var third = (await rig.Up(Token)).Value;

        Assert.Equal(["config", "start"], second.Ran, StringComparer.Ordinal);
        Assert.Equal(new string('0', 63) + "2", regenerated);
        Assert.Equal(["fetch", "config", "pull", "start"], third.Skipped, StringComparer.Ordinal);
        Assert.Equal(regenerated, OnyxConfig.ReadSecret(rig.Workspace.Files[rig.Paths.Env]));
        Assert.Equal(2, rig.Random.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_env_that_lost_only_its_auth_key_line_gets_a_new_one()
    {
        var rig = new OnyxUpRig();
        _ = await rig.Up(Token);
        rig.Workspace.Files[rig.Paths.Env] = "IMAGE_TAG=v4.9.0\nHOST_PORT=3000\n";

        var rerun = (await rig.Up(Token)).Value;

        Assert.Equal(["config", "start"], rerun.Ran, StringComparer.Ordinal);
        Assert.NotNull(OnyxConfig.ReadSecret(rig.Workspace.Files[rig.Paths.Env]));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_present_auth_key_is_kept_when_only_the_override_is_gone()
    {
        var rig = new OnyxUpRig();
        _ = await rig.Up(Token);
        var key = OnyxConfig.ReadSecret(rig.Workspace.Files[rig.Paths.Env]);
        _ = rig.Workspace.Files.Remove(rig.Paths.Override);

        var rerun = (await rig.Up(Token)).Value;

        Assert.Equal(["config", "start"], rerun.Ran, StringComparer.Ordinal);
        Assert.Equal(key, OnyxConfig.ReadSecret(rig.Workspace.Files[rig.Paths.Env]));
        Assert.Equal(1, rig.Random.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_corrupt_state_reruns_every_step_and_writes_a_fresh_state()
    {
        var rig = new OnyxUpRig();
        _ = await rig.Up(Token);
        var key = OnyxConfig.ReadSecret(rig.Workspace.Files[rig.Paths.Env]);
        rig.Workspace.StateIsCorrupt = true;

        var rerun = (await rig.Up(Token)).Value;

        Assert.Equal(["fetch", "config", "pull", "start"], rerun.Ran, StringComparer.Ordinal);
        Assert.Contains(rig.Progress.Lines, static l => l.Contains("state.json is unreadable", StringComparison.Ordinal));
        Assert.False(rig.Workspace.StateIsCorrupt);
        Assert.Equal(4, rig.Workspace.State!.Steps.Count);
        Assert.Equal(key, OnyxConfig.ReadSecret(rig.Workspace.Files[rig.Paths.Env]));
    }

    private static async Task WaitForProbes(OnyxUpRig rig, int count)
    {
        for (var i = 0; i < 1000 && rig.Health.Probed.Count < count; i++)
        {
            await Task.Delay(5, Token);
        }

        Assert.True(rig.Health.Probed.Count >= count, "the readiness wait never probed");
    }
}
