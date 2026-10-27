using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;

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
/// <param name="RowId">The needs-you row the notice is about ('desk:', 'park:' or 'takeover:' …); null for a recipe's notice and for lines written before it existed.</param>
/// <param name="DeepLink">Where a click on the notice goes ('needs-you/&lt;rowId&gt;'); null when it has no row.</param>
/// <param name="ItemTitle">The item's title, which the banner shows as its title instead of the instance address; null when it is not known (a banner then says just 'Sindri').</param>
public sealed record DeskNotice(
    string Identity,
    InstanceAddress Instance,
    ItemKey Item,
    NoticeClass Class,
    string Text,
    DateTimeOffset At,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NeedsYouRowId? RowId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DeepLink = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ItemTitle = null);
