using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

[Lane(TestLane.Unit)]
public sealed class InMemoryRecipeFilesContractTests : RecipeFilesContract
{
    private readonly InMemoryRecipeFiles _files = new();

    protected override IRecipeFiles Files => _files;

    protected override string Put(string name, string text)
    {
        var path = "/recipes/" + name;
        _ = _files.With(path, text);
        return path;
    }

    protected override string Missing(string name) => "/recipes/missing/" + name;
}
