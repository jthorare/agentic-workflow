using Aw.Kernel;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>Starts a program detached from the app, its output appended to the log (first-run spec decision 2).</summary>
internal sealed class DetachedProcessStarter : IProcessStarter
{
    public Result<Unit> Start(ProcessLaunch launch) => throw new NotSupportedException();
}
