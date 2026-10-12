using Aw.Sindri.Core.Workflow;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>The contract every <see cref="IRecipeFiles"/> honours, run against the in-memory fake and the files on disk.</summary>
public abstract class RecipeFilesContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The files under test.</summary>
    protected abstract IRecipeFiles Files { get; }

    /// <summary>Puts a recipe file named <paramref name="name"/> in place.</summary>
    /// <param name="name">The file name.</param>
    /// <param name="text">Its text.</param>
    /// <returns>The path to read it by.</returns>
    protected abstract string Put(string name, string text);

    /// <summary>A path where no file is.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The path.</returns>
    protected abstract string Missing(string name);

    /// <summary>A file reads back as the text it holds, non-ASCII included.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_file_reads_back_as_its_text()
    {
        const string Text = "schema: 1\nid: workflow.read\ndescription: \"Grüße, ok\"\n";
        var path = Put("workflow.read.yaml", Text);

        Expect.Equal(Text, (await Files.Read(path, Token)).Value);
    }

    /// <summary>A missing file is SND-RECIPE-010 naming the path.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_missing_file_is_SND_RECIPE_010()
    {
        var path = Missing("workflow.none.yaml");

        var read = await Files.Read(path, Token);

        Expect.Equal(WorkflowErrors.RecipeUnreadable.Code, read.Error.Code);
        Expect.True(read.Error.Message.Contains(path, StringComparison.Ordinal));
    }
}
