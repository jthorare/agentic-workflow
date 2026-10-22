namespace Aw.Sindri.Core.Setup;

/// <summary>The environment's off switch of the automatic MCP registration (setup-once spec §5.4): AW_SINDRI_APP_NO_MCP=1. The button still works.</summary>
public interface IMcpAutoSwitch
{
    /// <summary>Gets a value indicating whether the environment turns the automatic registration off.</summary>
    public bool Off { get; }
}
