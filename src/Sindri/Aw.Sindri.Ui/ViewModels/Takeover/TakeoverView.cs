namespace Aw.Sindri.Ui.ViewModels.Takeover;

/// <summary>What a takeover came to, in words.</summary>
/// <param name="Line">The sentence: sending, the viewer's outcome, or the refusal with its code.</param>
/// <param name="Command">The attach command to copy when no terminal opened; empty otherwise.</param>
internal sealed record TakeoverView(string Line, string Command);
