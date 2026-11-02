using System.Text.Json;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;

namespace Aw.Sindri.Tests.Sandbox;

/// <summary>The credential port's data types (sandbox spec SB18; SB-0): plain data the pure plan builder consumes, round-tripping through JSON.</summary>
public sealed class CredentialPortTests
{
    [Fact, Trait("Lane", "unit")]
    public void PreparedLogin_is_plain_data()
    {
        var login = new PreparedLogin(
            ModelLoginSource.OwnerCopy,
            [new LoginMount("/home/thor/.claude", "/home/thor/.claude", MountMode.ReadOnly), new LoginMount("/run/x/login", "/home/thor/.claude-login", MountMode.ReadWrite)],
            new Dictionary<string, string>(StringComparer.Ordinal) { ["CLAUDE_CONFIG_DIR"] = "/home/thor/.claude-login" },
            ["/home/thor/.claude-login"],
            new LoginHandle("/run/x/login"));

        var read = JsonSerializer.Deserialize<PreparedLogin>(JsonSerializer.Serialize(login, SliceHarness.Json), SliceHarness.Json)!;

        Assert.Equal(login.Mounts, read.Mounts);
        Assert.Equal(login.Environment, read.Environment);
        Assert.Equal(login.DeniedReads, read.DeniedReads);
        Assert.Equal((login.Source, login.Handle), (read.Source, read.Handle));
    }

    [Fact, Trait("Lane", "unit")]
    public void LoginStatus_names_its_state_and_source()
    {
        var json = JsonSerializer.Serialize(new LoginStatus(LoginState.Expired, ModelLoginSource.SeparateLogin, "run sindri sandbox login"), SliceHarness.Json);

        Assert.Equal("{\"state\":\"expired\",\"source\":\"separate-login\",\"detail\":\"run sindri sandbox login\"}", json);
    }
}
