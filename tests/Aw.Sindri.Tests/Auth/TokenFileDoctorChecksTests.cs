using System.Runtime.Versioning;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Auth;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Auth;

/// <summary>Doctor warns when the human token file can be read by its group or by everyone (setup-once spec §4, "What stops a session").</summary>
public sealed class TokenFileDoctorChecksTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [UnsupportedOSPlatform("windows")]
    private async Task<IReadOnlyList<DoctorCheck>> Run(UnixFileMode? mode)
    {
        var settings = new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile", "profile.yaml"));
        if (mode is { } given)
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(settings.TokenPath)!);
            await File.WriteAllTextAsync(settings.TokenPath, "token\n", Token);
            File.SetUnixFileMode(settings.TokenPath, given);
        }

        return await new TokenFileDoctorChecks(settings).Run(Token);
    }

    [UnsupportedOSPlatform("windows")]
    [Theory, Trait("Lane", "integration")]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead)]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead)]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupWrite)]
    public async Task A_token_file_a_group_or_everyone_can_reach_is_a_warning_with_the_fix(UnixFileMode mode)
    {
        var check = Assert.Single(await Run(mode));

        Assert.Equal((CheckState.Warn, "setup:token"), (check.State, check.Name));
        Assert.StartsWith("chmod 600 ", check.Fix, StringComparison.Ordinal);
    }

    [UnsupportedOSPlatform("windows")]
    [Fact, Trait("Lane", "integration")]
    public async Task An_owner_only_token_file_is_ok()
    {
        var check = Assert.Single(await Run(UnixFileMode.UserRead | UnixFileMode.UserWrite));

        Assert.Equal((CheckState.Ok, "setup:token"), (check.State, check.Name));
    }

    [UnsupportedOSPlatform("windows")]
    [Fact, Trait("Lane", "integration")]
    public async Task No_token_file_means_no_row() => Assert.Empty(await Run(mode: null));

    [UnsupportedOSPlatform("windows")]
    [Fact, Trait("Lane", "integration")]
    public async Task A_dangling_token_link_is_a_warning_to_remove_it()
    {
        var settings = new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile", "profile.yaml"));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(settings.TokenPath)!);
        _ = File.CreateSymbolicLink(settings.TokenPath, Path.Combine(_home.Path, "nowhere"));

        var check = Assert.Single(await new TokenFileDoctorChecks(settings).Run(Token));

        Assert.Equal((CheckState.Warn, "setup:token"), (check.State, check.Name));
        Assert.Equal($"{settings.TokenPath} can't be read (a broken link, or no permission to look at it)", check.Detail);
        Assert.Equal($"rm {settings.TokenPath}; the next sindri serve writes a new one", check.Fix);
    }
}
