namespace Aw.Sindri.Toolkit.Tests;

/// <summary>ToolkitRunTests swap the process-wide Console.Out while serving, so they never run beside another test.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessConsole
{
    public const string Name = "process console";
}
