using Aw.Sindri.Core;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

public sealed class OnyxUpTests
{
    private const long Gib = 1L << 30;
    private static readonly string[] _allSteps = ["fetch", "config", "pull", "start"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_first_run_runs_all_five_steps_in_order()
    {
        var rig = new OnyxUpRig();

        var response = (await rig.Up(Token)).Value;

        Assert.Equal(["Inspect", "Ps", "MemoryUsedOutside", "ServerPlatform", "Pull", "RepoDigests", "RenderedConfig", "Up"], rig.Runtime.Calls, StringComparer.Ordinal);
        Assert.Equal(_allSteps, response.Ran, StringComparer.Ordinal);
        Assert.Empty(response.Skipped);
        Assert.Equal("http://127.0.0.1:3000", response.Url);
        Assert.Equal(3000, response.Port);
        Assert.Equal("v4.9.0", response.Version);
        var state = rig.Workspace.State!;
        Assert.Equal(3000, state.Port);
        Assert.Equal(_allSteps.Order(StringComparer.Ordinal), state.Steps.Keys.Order(StringComparer.Ordinal));
        var env = rig.Workspace.Files[rig.Paths.Env];
        Assert.Contains("HOST_PORT=3000\n", env, StringComparison.Ordinal);
        Assert.Contains("USER_AUTH_SECRET=" + new string('0', 63) + "1\n", env, StringComparison.Ordinal);
        Assert.Contains("\"127.0.0.1:3000:80\"", rig.Workspace.Files[rig.Paths.Override], StringComparison.Ordinal);
        Assert.Contains(("" + rig.Paths.Env, true), rig.Workspace.Writes);
        Assert.False(rig.Workspace.LockHeld);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_run_calls_nothing_heavy_and_writes_nothing()
    {
        var rig = new OnyxUpRig();
        var first = (await rig.Up(Token)).Value;
        var env = rig.Workspace.Files[rig.Paths.Env];
        var (writes, stateWrites, fetches, calls, requested) = (rig.Workspace.Writes.Count, rig.Workspace.StateWrites, rig.Files.Fetches, rig.Runtime.Calls.Count, rig.Files.Requested.Count);

        var second = (await rig.Up(Token)).Value;

        Assert.Equal(_allSteps, second.Skipped, StringComparer.Ordinal);
        Assert.Empty(second.Ran);
        Assert.Equal(first.Port, second.Port);
        Assert.Equal(writes, rig.Workspace.Writes.Count);
        Assert.Equal(stateWrites, rig.Workspace.StateWrites);
        Assert.Equal(fetches + 1, rig.Files.Fetches);   // the pinned files are verified on every run
        Assert.Equal(requested, rig.Files.Requested.Count);   // and nothing is downloaded when they are intact
        Assert.Equal(env, rig.Workspace.Files[rig.Paths.Env]);
        var again = rig.Runtime.Calls.Skip(calls).ToList();
        Assert.DoesNotContain("Pull", again, StringComparer.Ordinal);
        Assert.DoesNotContain("Up", again, StringComparer.Ordinal);
        Assert.DoesNotContain("RepoDigests", again, StringComparer.Ordinal);
        Assert.Equal(1, rig.Random.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unreadable_env_file_is_SND_ONYX_012_and_starts_nothing()
    {
        var rig = new OnyxUpRig();
        _ = rig.Workspace.Unreadable.Add(rig.Paths.Env);

        var result = await rig.Up(Token);

        Assert.Equal("SND-ONYX-012", result.IsError ? result.Error.Code : "no error: ran " + string.Join(',', result.Value.Ran));
        Assert.DoesNotContain("Up", rig.Runtime.Calls, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_config_repair_that_rewrote_a_file_restarts_the_stack_so_changed_services_are_recreated()
    {
        var rig = new OnyxUpRig();
        _ = (await rig.Up(Token)).Value;
        rig.Workspace.Files[rig.Paths.Env] = rig.Workspace.Files[rig.Paths.Env].Replace("DISABLE_TELEMETRY=true", "DISABLE_TELEMETRY=false", StringComparison.Ordinal);
        rig.Runtime.Calls.Clear();

        var second = (await rig.Up(Token)).Value;

        Assert.Contains("DISABLE_TELEMETRY=true", rig.Workspace.Files[rig.Paths.Env], StringComparison.Ordinal);
        Assert.Contains("Up", rig.Runtime.Calls, StringComparer.Ordinal);
        Assert.Equal(["config", "start"], second.Ran, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rerun_that_rewrote_nothing_does_not_touch_the_running_stack()
    {
        var rig = new OnyxUpRig();
        _ = (await rig.Up(Token)).Value;
        rig.Runtime.Calls.Clear();

        _ = (await rig.Up(Token)).Value;

        Assert.DoesNotContain("Up", rig.Runtime.Calls, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_fail_row_blocks_before_any_fetch()
    {
        var rig = new OnyxUpRig();
        rig.Probes.FreeBytes = Gib;

        var error = (await rig.Up(Token)).Error;

        Assert.Equal("SND-ONYX-003", error.Code);
        Assert.Equal(0, rig.Files.Fetches);
        Assert.Empty(rig.Workspace.Writes);
        Assert.Equal(0, rig.Workspace.StateWrites);
        Assert.Equal(["Inspect", "Ps", "MemoryUsedOutside"], rig.Runtime.Calls, StringComparer.Ordinal);   // Ps: whether our own nginx already holds the port, by the project label with no files yet
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_warn_row_does_not_block_and_is_reported()
    {
        var rig = new OnyxUpRig();
        rig.Runtime.Facts = FakeContainerRuntime.Healthy with { MemoryBytes = null };

        var response = (await rig.Up(Token)).Value;

        var warning = Assert.Single(response.Warnings);
        Assert.StartsWith("memory: ", warning, StringComparison.Ordinal);
        Assert.Contains(warning, rig.Progress.Lines, StringComparer.Ordinal);
        Assert.Equal(_allSteps, response.Ran, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_remote_docker_endpoint_is_010_before_anything()
    {
        var rig = new OnyxUpRig();
        rig.Runtime.Facts = FakeContainerRuntime.Healthy with { Endpoint = "tcp://10.0.0.5:2375" };

        var error = (await rig.Up(Token)).Error;

        Assert.Equal("SND-ONYX-010", error.Code);
        Assert.Contains("tcp://10.0.0.5:2375", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, rig.Files.Fetches);
        Assert.Empty(rig.Workspace.Writes);
        Assert.Equal(["Inspect"], rig.Runtime.Calls, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_remote_endpoint_is_refused_with_010_before_any_compose_ps_even_when_setup_already_ran()
    {
        var rig = new OnyxUpRig();
        _ = (await rig.Up(Token)).Value;
        rig.Runtime.Calls.Clear();
        rig.Runtime.Facts = FakeContainerRuntime.Healthy with { Endpoint = "tcp://10.0.0.5:2375" };

        var error = (await rig.Up(Token)).Error;

        Assert.Equal("SND-ONYX-010", error.Code);
        Assert.DoesNotContain("Ps", rig.Runtime.Calls, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_up_while_the_first_holds_the_lock_is_busy()
    {
        var rig = new OnyxUpRig();
        using var held = rig.Workspace.TryLock().Value;

        var error = (await rig.Up(Token)).Error;

        Assert.Equal("SND-API-009", error.Code);
        Assert.Equal(0, rig.Files.Fetches);
        Assert.Empty(rig.Runtime.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_taken_preferred_port_moves_setup_to_the_first_free_fallback()
    {
        var rig = new OnyxUpRig();
        _ = rig.Probes.TakenPorts.Add(3000);

        var response = (await rig.Up(Token)).Value;

        Assert.Equal(3001, response.Port);
        Assert.Contains("HOST_PORT=3001\n", rig.Workspace.Files[rig.Paths.Env], StringComparison.Ordinal);
        Assert.Contains("\"127.0.0.1:3001:80\"", rig.Workspace.Files[rig.Paths.Override], StringComparison.Ordinal);
        Assert.Equal(3001, rig.Workspace.State!.Port);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Other_containers_using_the_memory_block_before_any_fetch()
    {
        var rig = new OnyxUpRig();
        rig.Runtime.OutsideMemoryBytes = 12L << 30;

        var error = (await rig.Up(Token)).Error;

        Assert.Equal("SND-ONYX-003", error.Code);
        Assert.Contains("other containers already use 12.0 GiB", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, rig.Files.Fetches);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_read_of_other_containers_memory_warns_and_the_start_goes_on()
    {
        var rig = new OnyxUpRig();
        rig.Runtime.FailNext("MemoryUsedOutside", SindriErrors.OnyxComposeFailed.With("`docker stats` failed (exit 1): boom"));

        var response = (await rig.Up(Token)).Value;

        Assert.Contains(response.Warnings, static w => w.StartsWith("memory:", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Progress_names_each_step_as_it_runs()
    {
        var rig = new OnyxUpRig();

        _ = await rig.Up(Token);

        Assert.Equal(["fetch: running", "config: running", "pull: running", "start: running"], rig.Progress.Lines.Where(static l => l.EndsWith(": running", StringComparison.Ordinal)), StringComparer.Ordinal);
    }
}
