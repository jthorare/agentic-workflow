using System.Reflection;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Infrastructure.Sandbox;

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
        var left = LeftBehind<OwnerCopyCredentialSourceContractTests>(source =>
        {
            var options = typeof(OwnerCopyCredentialSource).GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(source)!;
            return (string)options.GetType().GetProperty("Home")!.GetValue(options)!;
        });

        Assert.False(left, "the home is still on disk after the contract class was disposed");
    }

    /// <summary>The separate-login contract class leaves no /tmp/aw-separate-login-* home once disposed.</summary>
    [Fact]
    public void Separate_login_contract_leaves_no_temp_home()
    {
        var left = LeftBehind<SeparateLoginCredentialSourceContractTests>(source =>
            (string)source.GetType().GetField("_sindriHome", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(source)!);

        Assert.False(left, "the home is still on disk after the contract class was disposed");
    }

    private static bool LeftBehind<T>(Func<IModelCredentialSource, string> homeOf)
        where T : new()
    {
        var contract = new T();
        var create = typeof(T).GetMethod("Create", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var source = (IModelCredentialSource)create.Invoke(contract, [LoginState.Ready])!;
        var home = homeOf(source);
        Assert.True(Directory.Exists(home), "Create did not make a home to clean up");

        (contract as IDisposable)?.Dispose();
        var left = Directory.Exists(home);
        if (left)
        {
            // Today's leak: remove it here so the red run does not add to /tmp.
            Directory.Delete(home, recursive: true);
        }

        return left;
    }
}
