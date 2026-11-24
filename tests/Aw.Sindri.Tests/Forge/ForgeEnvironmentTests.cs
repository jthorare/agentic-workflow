using Aw.Sindri.Core.Connections;
using Aw.Sindri.Host.Forge;

namespace Aw.Sindri.Tests.Forge;

/// <summary>A device-flow connect writes no credential (first-run spec decision 14): the connected github row backs the forge's default AW_FORGE_TOKEN, and a set variable still wins.</summary>
public sealed class ForgeEnvironmentTests
{
    private static readonly Credentials _connected = new("gho_connected");

    [Fact, Trait("Lane", "unit")]
    public void An_unset_forge_token_reads_the_connected_github_row()
        => Assert.Equal("gho_connected", ForgeEnvironment.WithConnection(static _ => null, _connected)("AW_FORGE_TOKEN"));

    [Fact, Trait("Lane", "unit")]
    public void A_set_forge_token_wins_and_other_names_read_the_environment_alone()
    {
        var environment = ForgeEnvironment.WithConnection(static name => string.Equals(name, "AW_FORGE_TOKEN", StringComparison.Ordinal) ? "gho_env" : null, _connected);

        Assert.Equal("gho_env", environment("AW_FORGE_TOKEN"));
        Assert.Null(environment("AW_STATE_DIR"));
    }

    private sealed class Credentials(string token) : IConnectionCredentials
    {
        public string? TokenFor(string preset, string? profilePointer) => string.Equals(preset, "github", StringComparison.Ordinal) ? token : null;

        public string? TokenOf(Connection connection) => null;
    }
}
