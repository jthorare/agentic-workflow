namespace Aw.Sindri.Tests.Workflow.Blocks.Input;

/// <summary>The collection the 20,000-anchor gather runs in: nothing else runs beside it.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class HugeMap
{
    public const string Name = "huge map";
}
