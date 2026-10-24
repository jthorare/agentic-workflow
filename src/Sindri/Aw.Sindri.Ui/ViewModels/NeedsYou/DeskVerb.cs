namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>A recipe verb the desk request accepts now: the instance's own word for it, and whether it asks first.</summary>
/// <param name="Verb">The verb as served ("merge", "check", "rework", "comments", "resync-docs"); any other is shown as served.</param>
/// <param name="Cli">The 'sindri workflow command …' line the verb stands for.</param>
/// <param name="Destructive">True when the verb asks first: the served flag; a missing flag is not destructive.</param>
internal sealed record DeskVerb(string Verb, string Cli, bool Destructive);
