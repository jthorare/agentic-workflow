namespace Aw.Context.Core.Extract;

/// <summary>A directory of the path list, with its files and subdirectories counted, while <see cref="HomeFeatures"/> plans.</summary>
internal sealed class HomeDir(string path)
{
    public string Path { get; } = path;

    public List<string> Loose { get; } = [];

    public Dictionary<string, HomeDir> Children { get; } = new(StringComparer.Ordinal);

    public int Total { get; private set; }

    public void Add(string relative, string file)
    {
        Total++;
        var slash = relative.IndexOf('/', StringComparison.Ordinal);
        if (slash < 0)
        {
            Loose.Add(file);
            return;
        }

        var name = relative[..slash];
        if (!Children.TryGetValue(name, out var child))
        {
            child = new HomeDir(Path.Length == 0 ? name : Path + "/" + name);
            Children[name] = child;
        }

        child.Add(relative[(slash + 1)..], file);
    }

    public List<string> AllFiles()
    {
        var files = new List<string>(Total);
        Collect(files);
        files.Sort(StringComparer.Ordinal);
        return files;
    }

    private void Collect(List<string> into)
    {
        into.AddRange(Loose);
        foreach (var child in Children.Values)
        {
            child.Collect(into);
        }
    }
}
