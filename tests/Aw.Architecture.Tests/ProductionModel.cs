using Aw.Architecture.Tests;

[assembly: AssemblyFixture(typeof(ProductionModel))]

namespace Aw.Architecture.Tests;

/// <summary>
/// Loads the architecture model and its edges once, before any test runs. Without it the first rule in each class to
/// run in parallel blocks on the shared load, and the lane cap bills that test for all of it (62-76 s on the CI
/// runner against the 60 s integration cap, issue 1214).
/// </summary>
public sealed class ProductionModel
{
    /// <summary>Builds <see cref="Production.Architecture"/> and <see cref="Production.Edges"/>.</summary>
    public ProductionModel()
    {
        _ = Production.Edges.Count;
    }
}
