using System.Runtime.Versioning;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Auth;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Secrets;

namespace Aw.Sindri.Tests.Auth;

/// <summary>A dangling link answers File.Exists with true but File.GetUnixFileMode follows it and throws (#2233): every permission check says what it found instead of throwing.</summary>
[UnsupportedOSPlatform("windows")]
public sealed class DanglingLinkModeTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Dangling(params string[] segments)
    {
        var path = Path.Combine([_home.Path, .. segments]);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _ = File.CreateSymbolicLink(path, Path.Combine(_home.Path, "nowhere"));
        return path;
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Doctor_answers_a_dangling_token_link_with_a_warn_row()
    {
        var settings = new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile", "profile.yaml"));
        _ = Dangling(Path.GetRelativePath(_home.Path, settings.TokenPath));

        var check = Assert.Single(await new TokenFileDoctorChecks(settings).Run(Token));

        Assert.Equal((CheckState.Warn, "setup:token"), (check.State, check.Name));
        Assert.Contains(settings.TokenPath, check.Detail, StringComparison.Ordinal);
        Assert.NotNull(check.Fix);
        Assert.Contains(settings.TokenPath, check.Fix, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public void Launcher_warnings_name_a_dangling_launcher_file()
    {
        var path = Dangling("launcher.json");

        var warnings = LauncherFilePermissions.Warnings(path);

        Assert.Contains(warnings, warning => warning.Contains(path, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public void A_file_pointer_at_a_dangling_link_is_refused_not_thrown()
    {
        var link = Dangling("token-link");
        var secrets = new PointerSecrets(_ => null, new FakeToolRunner(ToolRun.Missing));

        var result = secrets.Resolve(PointerSecretsTests.Pointer("file:" + link));

        Assert.False(result.IsSuccess);
        Assert.Equal(SindriErrors.SecretFileUnsafe.Code, result.Error.Code);
    }
}
