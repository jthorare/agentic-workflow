using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Infrastructure.Toolkits;

internal sealed partial class ToolkitSupervisor
{
    private sealed class Running(IToolkitProcess first, Toolkit toolkit)
    {
        internal Toolkit Toolkit { get; } = toolkit;

        internal IToolkitProcess? Process { get; set; } = first;

        internal Task Watcher { get; set; } = Task.CompletedTask;

        /// <summary>Whether calls are refused and the tools hidden; the process keeps running.</summary>
        internal volatile bool Paused;

        /// <summary>Set once the watcher has decided to give up, before it queues on the gate.</summary>
        internal volatile bool GivingUp;

        internal CancellationTokenSource Stopping { get; } = new();
    }
}
