using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Features.CheckSandbox;

/// <summary>The machine as the runtime sees it: its available memory (container-aware) and processor count.</summary>
internal sealed class ProcessSandboxMachine : ISandboxMachine
{
    public long RamBytes => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

    public int Cores => Environment.ProcessorCount;
}
