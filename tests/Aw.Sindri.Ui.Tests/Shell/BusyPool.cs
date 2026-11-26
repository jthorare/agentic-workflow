namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>Tests that hold every thread-pool thread for a moment; they run alone, after the parallel ones, so no other test waits on the pool.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BusyPool
{
    /// <summary>The collection's name.</summary>
    public const string Name = "Busy thread pool";
}
