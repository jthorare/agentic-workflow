using Aw.Kernel;

namespace Aw.Sindri.Core.Control;

/// <summary>Starts the control plane as a detached background process (port; the planned Launcher seam, §17.20).</summary>
public interface IControlPlaneLauncher
{
    /// <summary>Starts 'sindri serve' detached from this terminal, its output appended to a log file.</summary>
    /// <returns>The started process, or SND-CLI-002 when it can't be started.</returns>
    public Result<ILaunchedProcess> Launch();
}
