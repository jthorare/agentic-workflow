namespace Aw.Judge.Features.SetPromptSort;

/// <summary>One scaffold's switch.</summary>
/// <param name="Scaffold">The scaffold id: brief, bugfix, ui-evidence or plan-first.</param>
/// <param name="On">Whether it fires.</param>
public sealed record ScaffoldSwitch(string Scaffold, bool On);
