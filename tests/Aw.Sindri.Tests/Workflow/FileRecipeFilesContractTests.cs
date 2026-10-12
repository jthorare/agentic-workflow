using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

[Lane(TestLane.Integration)]
public sealed class FileRecipeFilesContractTests : RecipeFilesContract, IDisposable
{
    private readonly RecipeDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    protected override IRecipeFiles Files { get; } = new FileRecipeFiles();

    protected override string Put(string name, string text) => _directory.Write(name, text);

    protected override string Missing(string name) => Path.Combine(_directory.Path, "missing", name);

    /// <summary>A directory exists, so SND-RECIPE-010 says it isn't a file rather than that it doesn't exist.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_directory_is_SND_RECIPE_010_saying_it_is_a_directory()
    {
        var path = Directory.CreateDirectory(Path.Combine(_directory.Path, "workflow.dir.yaml")).FullName;

        var read = await Files.Read(path, TestContext.Current.CancellationToken);

        Expect.Equal(WorkflowErrors.RecipeUnreadable.Code, read.Error.Code);
        Expect.Equal($"{path} is a directory, not a recipe file (fix: name the recipe's .yaml file)", read.Error.Message);
    }
}
