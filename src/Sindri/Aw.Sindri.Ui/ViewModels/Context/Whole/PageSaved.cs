namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>What <see cref="IPageWrites.Save"/> did.</summary>
/// <param name="State">How it ended.</param>
/// <param name="Code">The error id when refused or failed, else null.</param>
/// <param name="Message">The server's sentence when refused ("index.pages[0].actions[1].concepts[0]: 'aproval' is no concept in map v41 (fix: …)"), else null.</param>
internal sealed record PageSaved(PageSavedState State, string? Code = null, string? Message = null);
