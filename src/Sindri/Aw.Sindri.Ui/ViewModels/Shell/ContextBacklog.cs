namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Context index card's backlog bar: what is embedded of everything, and its sentence.</summary>
/// <param name="Embedded">Records with a vector.</param>
/// <param name="Total">Embedded plus pending.</param>
/// <param name="Text">"30 of 60 records embedded".</param>
internal sealed record ContextBacklog(int Embedded, int Total, string Text);
