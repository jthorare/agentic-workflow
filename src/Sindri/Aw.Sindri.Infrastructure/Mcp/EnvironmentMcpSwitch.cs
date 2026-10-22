using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Infrastructure.Mcp;

/// <summary>AW_SINDRI_APP_NO_MCP=1 turns the automatic MCP registration off (setup-once spec §5.4); anything else leaves it on.</summary>
/// <param name="environment">Reads an environment variable.</param>
internal sealed class EnvironmentMcpSwitch(Func<string, string?> environment) : IMcpAutoSwitch
{
    /// <inheritdoc/>
    public bool Off => string.Equals(environment("AW_SINDRI_APP_NO_MCP"), "1", StringComparison.Ordinal);
}
