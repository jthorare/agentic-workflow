namespace Aw.Sindri.Core.Events;

/// <summary>The 'workflow.retire.done' payload (issue 2258 PR 3): the retire pass finished. Key 'workflow:retire-pass:done'; written once, even when it retired nothing.</summary>
/// <param name="Retired">How many instances the pass gave a 'workflow.retired' note.</param>
public sealed record RetirePassDone(int Retired);
