namespace Aw.Sindri.Tests;

/// <summary>LoopbackBindingTests set process environment variables, so they never run beside another test.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessEnvironment
{
    public const string Name = "process environment";
}
