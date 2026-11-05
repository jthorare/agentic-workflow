using Aw.Sindri.Core.ModelCheck;

namespace Aw.Sindri.Features.ModelCheck;

/// <summary>A spec folder with its README read.</summary>
/// <param name="Folder">The folder.</param>
/// <param name="Readme">What its README says.</param>
internal sealed record Spec(SpecFolder Folder, ModelReadme Readme);
