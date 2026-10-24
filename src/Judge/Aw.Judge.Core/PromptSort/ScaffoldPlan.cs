namespace Aw.Judge.Core.PromptSort;

/// <summary>Which scaffolds the sorted prompt calls for, which of them fire, and which were held back.</summary>
/// <param name="WouldFire">The scaffolds that would fire were their switches on and no other rule held them back.</param>
/// <param name="Fired">The scaffolds that fire.</param>
/// <param name="Suppressed">The scaffolds held back, each as a JSON object '{"id":…,"reason":…}'.</param>
/// <param name="RequiresUiEvidence">Whether the session now needs UI evidence before a done claim.</param>
/// <param name="Context">The note to add to the prompt: empty when nothing fires.</param>
public sealed record ScaffoldPlan(IReadOnlyList<string> WouldFire, IReadOnlyList<string> Fired, IReadOnlyList<string> Suppressed, bool RequiresUiEvidence, string Context);
