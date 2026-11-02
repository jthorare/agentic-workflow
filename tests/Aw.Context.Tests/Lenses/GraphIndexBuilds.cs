namespace Aw.Context.Tests.Lenses;

/// <summary>Counts process-wide graph index builds, so its tests never run beside another test that builds one.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GraphIndexBuilds
{
    public const string Name = "graph index builds";
}
