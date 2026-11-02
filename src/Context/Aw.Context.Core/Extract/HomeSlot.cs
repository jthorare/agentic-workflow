namespace Aw.Context.Core.Extract;

/// <summary>A directory (or its loose files) that becomes one home or group feature while <see cref="HomeFeatures"/> plans.</summary>
internal sealed class HomeSlot(string path, bool loose, bool group, List<string> files, HomeSlot? parent)
{
    public string Path { get; } = path;

    public bool Loose { get; } = loose;

    public bool Group { get; } = group;

    public List<string> Files { get; } = files;

    public HomeSlot? Parent { get; } = parent;

    // The pin the leaf's files all belong to; Suffixed when the pin split a leaf, so its key ends in '~<id>'.
    public string? PinId { get; set; }

    public bool Suffixed { get; init; }

    // The pin suffix of the key: '~<id>' with a '.' in the id written '_'.
    public string KeySuffix => Suffixed && PinId is { } id ? "~" + id.Replace('.', '_') : "";

    public string Identity => (Loose ? Path + "\0loose" : Path) + (Suffixed ? "\0pin:" + PinId : "");
}
