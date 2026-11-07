using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Secrets;

/// <summary>KeychainSecretWriter.DeleteFromKeychain: the entry goes through the OS tool, and a keychain that refuses is an error (SND-SECRET-004).</summary>
[Lane(TestLane.Unit)]
public sealed class KeychainDeleteTests
{
    private static KeychainSecretWriter Writer(FakeToolRunner runner, KeychainPlatform platform) => new(runner, new FakeSindriHome(Path.Combine(Path.GetTempPath(), "aw-keychain-delete-unused")), platform);

    [Fact]
    public void On_Linux_secret_tool_clears_the_entry_and_the_key_is_not_on_the_command_line()
    {
        var runner = new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 0, ""));

        var result = Writer(runner, KeychainPlatform.Linux).DeleteFromKeychain("sindri-index", "linear-write");

        Assert.True(result.IsSuccess);
        var call = Expect.Only(runner.Calls);
        Expect.Equal("secret-tool", call.FileName);
        Expect.Sequence(["clear", "service", "sindri-index", "account", "linear-write"], call.Arguments);
    }

    [Fact]
    public void On_macOS_an_entry_that_is_not_there_counts_as_deleted()
    {
        var runner = new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 44, ""));

        Assert.True(Writer(runner, KeychainPlatform.MacOS).DeleteFromKeychain("sindri-index", "linear-write").IsSuccess);
    }

    [Fact]
    public void A_keychain_that_refuses_or_is_missing_is_SND_SECRET_004()
    {
        var refused = Writer(new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 1, "")), KeychainPlatform.Linux).DeleteFromKeychain("sindri-index", "linear-write");
        var missing = Writer(new FakeToolRunner(ToolRun.Missing), KeychainPlatform.Linux).DeleteFromKeychain("sindri-index", "linear-write");

        Expect.Equal("SND-SECRET-004", refused.Error.Code);
        Expect.Equal("SND-SECRET-004", missing.Error.Code);
    }

    [Fact]
    public void The_fake_keychain_forgets_a_deleted_key()
    {
        var fake = new FakeSecretWriter();
        _ = fake.StoreInKeychain("sindri-index", "linear-write", "lin_api_write");

        Assert.True(fake.DeleteFromKeychain("sindri-index", "linear-write").IsSuccess);

        Assert.Empty(fake.Stored);
    }
}
