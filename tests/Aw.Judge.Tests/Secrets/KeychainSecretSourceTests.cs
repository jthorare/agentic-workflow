using Aw.Judge.Core.Secrets;
using Aw.Judge.Infrastructure.Secrets;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Secrets;

[Lane(TestLane.Unit)]
public sealed class KeychainSecretSourceTests
{
    private readonly FakeProcessRunner _runner = new();

    [Fact]
    public async Task The_environment_wins_and_the_keychain_is_not_asked()
    {
        _ = _runner.With("security", new FakeProgram { StandardOutput = "from-keychain\n" });

        var value = await Source(environmentKey: "from-environment").Read(SecretName.TypeSafeApiKey, TestContext.Current.CancellationToken);

        Assert.Equal("from-environment", value);
        Assert.Empty(_runner.Calls);
    }

    [Fact]
    public async Task Without_the_variable_the_key_comes_from_the_keychain_entry()
    {
        _ = _runner.With("security", new FakeProgram { StandardOutput = "from-keychain\n" });

        var value = await Source(environmentKey: "").Read(SecretName.TypeSafeApiKey, TestContext.Current.CancellationToken);

        Assert.Equal("from-keychain", value);
        var call = Assert.Single(_runner.Calls).Request;
        Assert.Equal(["find-generic-password", "-s", "agentic-workflow-typesafe", "-a", "TYPESAFE_API_KEY", "-w"], call.Arguments);
        Assert.Null(call.StandardInput);
    }

    [Fact]
    public async Task A_keychain_without_the_entry_is_null()
    {
        _ = _runner.With("security", new FakeProgram { StandardError = "security: SecKeychainSearchCopyNext: The specified item could not be found in the keychain.", ExitCode = 44 });

        Assert.Null(await Source(environmentKey: null).Read(SecretName.TypeSafeApiKey, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_keychain_that_hangs_is_null()
    {
        _ = _runner.With("security", new FakeProgram { Runs = TimeSpan.FromMinutes(1), StandardOutput = "late" });

        Assert.Null(await Source(environmentKey: null).Read(SecretName.TypeSafeApiKey, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task No_keychain_tool_is_null() =>
        Assert.Null(await Source(environmentKey: null).Read(SecretName.TypeSafeApiKey, TestContext.Current.CancellationToken));

    private KeychainSecretSource Source(string? environmentKey) =>
        new(name => name == "TYPESAFE_API_KEY" ? environmentKey : null, _runner);
}
