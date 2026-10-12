namespace Aw.Sindri.Tests.Workflow;

/// <summary>A temporary directory of recipe files, deleted on dispose.</summary>
internal sealed class RecipeDirectory : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("aw-sindri-recipes-").FullName;

    public string Write(string name, string text)
    {
        var path = System.IO.Path.Combine(Path, name);
        File.WriteAllText(path, text);
        return path;
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
