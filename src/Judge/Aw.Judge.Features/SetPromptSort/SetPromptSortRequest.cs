namespace Aw.Judge.Features.SetPromptSort;

/// <summary>
/// The SetPromptSort request: 'judge config set prompt-sort enabled true|false' and 'judge prompt-sort scaffold &lt;id&gt; on|off'
/// (and, after the switch, 'prompt-sort promote --apply'). Exactly one of the two is set.
/// </summary>
/// <param name="Enabled">The new 'promptSort.enabled', or null.</param>
/// <param name="Scaffold">The scaffold switch to write into 'promptSort.scaffolds', or null.</param>
public sealed record SetPromptSortRequest(bool? Enabled = null, ScaffoldSwitch? Scaffold = null);
