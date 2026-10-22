using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>One thing to do after a run ended.</summary>
/// <param name="Text">The words.</param>
/// <param name="Link">Where it goes.</param>
internal sealed record RunNextOption(string Text, DeepLink Link);
