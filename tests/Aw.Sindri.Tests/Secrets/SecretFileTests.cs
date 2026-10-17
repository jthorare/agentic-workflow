using System.Runtime.Versioning;
using Aw.Sindri.Core;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Secrets;

[UnsupportedOSPlatform("windows")]
public sealed class SecretFileTests : IDisposable
{
    private const string Fixture = "fixture-file-token";
    private const UnixFileMode OwnerReadWrite = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private readonly TempHome _home = new();
    private readonly PointerSecrets _secrets = new(_ => null, new FakeToolRunner(ToolRun.Missing));

    public void Dispose() => _home.Dispose();

    [Theory, Trait("Lane", "integration")]
    [InlineData(Fixture)]
    [InlineData(Fixture + "\n")]
    [InlineData(Fixture + "\r\n")]
    public void A_0600_file_resolves_without_one_trailing_newline(string contents)
    {
        var path = Write(contents, OwnerReadWrite);

        Assert.Equal(Fixture, _secrets.Resolve(PointerSecretsTests.Pointer("file:" + path)).Value);
    }

    [Fact, Trait("Lane", "integration")]
    public void A_0400_file_resolves()
        => Assert.Equal(Fixture, _secrets.Resolve(PointerSecretsTests.Pointer("file:" + Write(Fixture, UnixFileMode.UserRead))).Value);

    [Theory, Trait("Lane", "integration")]
    [InlineData(UnixFileMode.GroupRead)]
    [InlineData(UnixFileMode.OtherRead)]
    [InlineData(UnixFileMode.OtherWrite)]
    public void A_file_others_may_touch_is_SND_SECRET_003(UnixFileMode extra)
    {
        var path = Write(Fixture, OwnerReadWrite | extra);

        var result = _secrets.Resolve(PointerSecretsTests.Pointer("file:" + path));

        Assert.Equal(SindriErrors.SecretFileUnsafe.Code, result.Error.Code);
        Assert.Contains(path, result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Fixture, result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public void A_file_under_a_directory_the_owner_cannot_search_is_an_error_not_an_exception()
    {
        Assert.SkipWhen(Environment.IsPrivilegedProcess, "a privileged process ignores the permissions this test takes away");
        var locked = Path.Combine(_home.Path, "locked");
        var path = Path.Combine(locked, "token");
        _ = Directory.CreateDirectory(locked);
        File.WriteAllText(path, Fixture);
        File.SetUnixFileMode(locked, UnixFileMode.None);
        try
        {
            var result = _secrets.Resolve(PointerSecretsTests.Pointer("file:" + path));

            Assert.False(result.IsSuccess);
            Assert.Equal(SindriErrors.SecretUnresolved.Code, result.Error.Code);
            Assert.Contains("doesn't exist or can't be reached", result.Error.Message, StringComparison.Ordinal);
            Assert.Contains("check the directory's owner and mode", result.Error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(Fixture, result.Error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact, Trait("Lane", "integration")]
    public void A_missing_file_is_SND_SECRET_002()
    {
        var result = _secrets.Resolve(PointerSecretsTests.Pointer("file:" + Path.Combine(_home.Path, "absent")));

        Assert.Equal(SindriErrors.SecretUnresolved.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public void A_directory_is_SND_SECRET_003()
    {
        var result = _secrets.Resolve(PointerSecretsTests.Pointer("file:" + _home.Path));

        Assert.Equal(SindriErrors.SecretFileUnsafe.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public void A_symlink_is_SND_SECRET_003_even_to_a_0600_file()
    {
        var target = Write(Fixture, OwnerReadWrite);
        var link = Path.Combine(_home.Path, "token-link");
        _ = File.CreateSymbolicLink(link, target);

        var result = _secrets.Resolve(PointerSecretsTests.Pointer("file:" + link));

        Assert.Equal(SindriErrors.SecretFileUnsafe.Code, result.Error.Code);
        Assert.Contains($"{link} is not a regular file", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Fixture, result.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("")]
    [InlineData("\n")]
    public void An_empty_file_is_SND_SECRET_002(string contents)
    {
        var result = _secrets.Resolve(PointerSecretsTests.Pointer("file:" + Write(contents, OwnerReadWrite)));

        Assert.Equal(SindriErrors.SecretUnresolved.Code, result.Error.Code);
    }

    private string Write(string contents, UnixFileMode mode)
    {
        var path = Path.Combine(_home.Path, "token");
        File.WriteAllText(path, contents);
        File.SetUnixFileMode(path, mode);
        return path;
    }
}
