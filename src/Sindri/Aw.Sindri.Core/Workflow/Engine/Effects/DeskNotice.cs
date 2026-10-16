namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>
/// One line of '$AW_SINDRI_HOME/notifications.jsonl' (engine spec §6.3; README X5, X50: 'notify.desk' is the only OS
/// notifier; engine P6 unit (c)).
/// </summary>
/// <param name="Identity">The effect identity: the notifier writes one line per identity, so a repeat is harmless.</param>
/// <param name="Instance">The instance address.</param>
/// <param name="Item">The item key.</param>
/// <param name="Class">Interrupt or info.</param>
/// <param name="Text">What the notice says.</param>
/// <param name="At">When it was sent.</param>
public sealed record DeskNotice(string Identity, InstanceAddress Instance, ItemKey Item, NoticeClass Class, string Text, DateTimeOffset At);
