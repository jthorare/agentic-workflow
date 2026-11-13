using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox.Credentials;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>
/// The credential-source contract classes own the fake homes they create, so disposing the test class removes them (#3262).
/// Each case runs the way the contract base runs one: construct the class, call Create, then dispose what the class owns.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ContractFixtureCleanupTests
{
    /// <summary>The owner-copy contract class leaves no /tmp/aw-owner-copy-* home once disposed.</summary>
    [Fact]
    public void Owner_copy_contract_leaves_no_temp_home()
    {
        var contract = new OwnerCopyCredentialSourceContractTests();
        _ = contract.CreateForTest(LoginState.Ready);
        var home = contract.HomePaths.Single();
        Assert.True(Directory.Exists(home), "Create did not make a home to clean up");

        contract.Dispose();

        Assert.False(Directory.Exists(home), "the home is still on disk after the contract class was disposed");
    }

    /// <summary>The separate-login contract class leaves no /tmp/aw-separate-login-* home once disposed.</summary>
    [Fact]
    public void Separate_login_contract_leaves_no_temp_home()
    {
        var contract = new SeparateLoginCredentialSourceContractTests();
        _ = contract.CreateForTest(LoginState.Ready);
        var home = contract.HomePaths.Single();
        Assert.True(Directory.Exists(home), "Create did not make a home to clean up");

        contract.Dispose();

        Assert.False(Directory.Exists(home), "the home is still on disk after the contract class was disposed");
    }

    /// <summary>A home already gone does not stop the owner-copy contract class from removing the others.</summary>
    [Fact]
    public void Owner_copy_contract_removes_every_home_when_one_is_already_gone()
    {
        var contract = new OwnerCopyCredentialSourceContractTests();
        _ = contract.CreateForTest(LoginState.Ready);
        _ = contract.CreateForTest(LoginState.Ready);
        var homes = contract.HomePaths;
        Directory.Delete(homes[0], recursive: true);

        contract.Dispose();

        Assert.False(Directory.Exists(homes[1]), "the second home leaked after the first was already gone");
    }

    /// <summary>A home already gone does not stop the separate-login contract class from removing the others.</summary>
    [Fact]
    public void Separate_login_contract_removes_every_home_when_one_is_already_gone()
    {
        var contract = new SeparateLoginCredentialSourceContractTests();
        _ = contract.CreateForTest(LoginState.Ready);
        _ = contract.CreateForTest(LoginState.Ready);
        var homes = contract.HomePaths;
        Directory.Delete(homes[0], recursive: true);

        contract.Dispose();

        Assert.False(Directory.Exists(homes[1]), "the second home leaked after the first was already gone");
    }

    /// <summary>Disposing a fake owner home whose directory is already gone does not throw.</summary>
    [Fact]
    public void Fake_owner_home_dispose_is_best_effort()
    {
        var home = new FakeOwnerHome(LoginState.Ready);
        Directory.Delete(home.Root, recursive: true);

        home.Dispose();
    }

    /// <summary>Disposing a fake separate home whose directory is already gone does not throw.</summary>
    [Fact]
    public void Fake_separate_home_dispose_is_best_effort()
    {
        var home = new FakeSeparateHome(LoginState.Ready);
        Directory.Delete(home.SindriHome, recursive: true);

        home.Dispose();
    }
}
