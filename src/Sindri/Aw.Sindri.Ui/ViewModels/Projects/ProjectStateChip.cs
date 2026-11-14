namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>One state chip on the sheet: the state and whether it is the one picked now (#3719).</summary>
/// <param name="Choice">The state.</param>
/// <param name="IsPicked">True for the chip that is the project's state.</param>
internal sealed record ProjectStateChip(ProjectStateChoice Choice, bool IsPicked)
{
    /// <summary>The chip's word.</summary>
    public string Word => Choice.Word;
}
