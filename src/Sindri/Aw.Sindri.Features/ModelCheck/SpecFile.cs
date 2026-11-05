namespace Aw.Sindri.Features.ModelCheck;

/// <summary>One entry of a spec folder.</summary>
/// <param name="Name">Its path inside the folder ('Takeover.cfg', 'gaps/PlantedBug.cfg').</param>
/// <param name="Kind">What it is on disk.</param>
/// <param name="Text">What it holds; empty for an entry that is not a regular file.</param>
internal sealed record SpecFile(string Name, SpecFileKind Kind, string Text);
