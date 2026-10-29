using System.Text.Json;
using Aw.Context.Testing.Engine;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Leftovers;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.Leftovers;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.RunDoctor;

/// <summary>Doctor's 'index:onyx-leftover' row (#1598): a failure while an old `onyx up` left something, a warning once the profile acknowledges it, no row when clean.</summary>
public sealed class OnyxLeftoverDoctorTests
{
    private const string Home = "/home/aw/sindri-app";
    private readonly SliceHarness _h = new();
    private readonly FakeDocker _docker = new();
    private readonly FakeFiles _files = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task Leftovers_fail_with_what_is_left_and_the_purge_fix()
    {
        _docker.Containers = ["sindri-onyx-0a1b2c3d-api-1", "sindri-onyx-0a1b2c3d-web-1"];
        _docker.Volumes = ["sindri-onyx-0a1b2c3d_db"];
        _files.Directories.Add(Home + "/onyx");

        var rows = await Doctor().Run(Token);

        Assert.Equal(new DoctorCheck("index:onyx-leftover", CheckState.Fail, "old Onyx left 2 container(s), 1 volume(s), the " + Home + "/onyx directory", "sindri onyx down --purge"), Assert.Single(rows));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Leftovers_warn_instead_once_the_approved_profile_acknowledges_them()
    {
        _files.Directories.Add(Home + "/onyx");
        await Approve("""{"index":{"acknowledgeOnyxLeftover":true}}""");

        var row = Assert.Single(await Doctor().Run(Token));

        Assert.Equal((CheckState.Warn, "old Onyx left the " + Home + "/onyx directory (acknowledged in context.index.acknowledgeOnyxLeftover)", "sindri onyx down --purge"), (row.State, row.Detail, row.Fix));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_profile_that_does_not_acknowledge_still_fails()
    {
        _files.Directories.Add(Home + "/onyx");
        await Approve("""{"index":{"acknowledgeOnyxLeftover":false}}""");

        Assert.Equal(CheckState.Fail, Assert.Single(await Doctor().Run(Token)).State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_clean_box_has_no_row()
    {
        Assert.Empty(await Doctor().Run(Token));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_docker_that_does_not_answer_warns_that_it_was_not_checked()
    {
        _docker.Failure = Core.SindriErrors.IndexOnyxLeftover.With("docker did not list what Onyx left");

        var row = Assert.Single(await Doctor().Run(Token));

        Assert.Equal((CheckState.Warn, "not checked: docker did not list what Onyx left", "sindri onyx down --purge"), (row.State, row.Detail, row.Fix));
    }

    private OnyxLeftoverDoctor Doctor() => new(_h.Profiles, FakeContextEngine.Instance, new FakeSindriHome(Home), _docker, _files);

    private async Task Approve(string context)
    {
        var profile = new SindriProfile(1, "thor", Context: JsonDocument.Parse(context).RootElement.Clone());
        _ = await _h.Profiles.Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), Token);
    }

    private sealed class FakeDocker : IPurgeDocker
    {
        internal IReadOnlyList<string> Containers { get; set; } = [];

        internal IReadOnlyList<string> Volumes { get; set; } = [];

        internal Error? Failure { get; set; }

        public Result<IReadOnlyList<string>> ContainersOf(string project) => Failure is { } error ? error : Containers.ToList();

        public Result<IReadOnlyList<string>> VolumesOf(string project) => Failure is { } error ? error : Volumes.ToList();

        public Result<Unit> RemoveContainers(IReadOnlyList<string> names) => throw new NotSupportedException();

        public Result<Unit> RemoveVolumes(IReadOnlyList<string> names) => throw new NotSupportedException();
    }

    private sealed class FakeFiles : ILeftoverFiles
    {
        internal HashSet<string> Directories { get; } = new(StringComparer.Ordinal);

        public string? ReadText(string path) => null;

        public bool DirectoryExists(string path) => Directories.Contains(path);

        public Result<Unit> DeleteDirectory(string path) => throw new NotSupportedException();
    }
}
