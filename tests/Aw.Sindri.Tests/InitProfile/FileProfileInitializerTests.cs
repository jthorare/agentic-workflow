using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Tests.Profile;

namespace Aw.Sindri.Tests.InitProfile;

/// <summary>The profile file 'profile init' writes: it loads as a valid profile with no repos, its folder is made, and a file that is there is never touched.</summary>
[Lane(TestLane.Integration)]
public sealed class FileProfileInitializerTests : IDisposable
{
    private static readonly string[] _effects = ["effect.claim.ref", "effect.notify.desk"];

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "aw-profile-init-" + RealTime.NewGuid().ToString("N"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string ProfilePath => Path.Combine(_dir, "profile", "profile.yaml");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private FileProfileInitializer Initializer() => new(new SindriSettings(_dir, ProfilePath));

    [Fact]
    public async Task It_creates_the_folder_and_a_profile_that_loads_with_the_effects_and_no_repos()
    {
        var done = await Initializer().Create(_effects, Token);

        Assert.True(done.IsSuccess, done.IsError ? done.Error.Message : null);
        Assert.Equal(ProfilePath, done.Value);
        Assert.Null(YamlToJson.TryConvert(await File.ReadAllTextAsync(ProfilePath, Token), out var json));
        var loaded = ProfileReading.FromJson(json, ProfilePath, ProfileFiles.Options);
        Assert.True(loaded.IsSuccess, loaded.IsSuccess ? null : loaded.Error.Message);
        Assert.Empty(loaded.Value.Profile.Repos ?? []);
        Assert.Equal(_effects, loaded.Value.Profile.Workflow?.Effects?.ToArray());
    }

    [Fact]
    public async Task A_file_that_is_there_is_refused_and_left_as_it_was()
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!);
        await File.WriteAllTextAsync(ProfilePath, "schemaVersion: 1\nuser: \"mine\"\n", Token);

        var done = await Initializer().Create(_effects, Token);

        Assert.Equal("SND-PROFILE-002", done.Error.Code);
        Assert.Equal("schemaVersion: 1\nuser: \"mine\"\n", await File.ReadAllTextAsync(ProfilePath, Token));
    }

    [Fact]
    public async Task A_folder_that_cannot_be_written_is_SND_PROFILE_002_with_the_reason()
    {
        _ = Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(Path.Combine(_dir, "profile"), "a file where the folder should be", Token);

        var done = await Initializer().Create(_effects, Token);

        Assert.Equal("SND-PROFILE-002", done.Error.Code);
        Assert.Contains("can't be written", done.Error.Message, StringComparison.Ordinal);
    }
}
